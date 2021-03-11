using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

using Assets.Scripts.ServerServiceHelper;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using ServerPixel = Assets.Scripts.ServerService.Pixel;
using ServerPixels = Assets.Scripts.ServerService.Pixels;
using ServerColor = Assets.Scripts.ServerService.Color;
using System;

public class PaintCanvas : MonoBehaviour
{
    //public RenderTexture rt;
    private Texture2D tex;
    private RectTransform rectT;
    public Color col;
    public int width = 64;
    public int height = 64;
    public ColorPicker colorPicker;
    public ColorPicker_Hue colorPickerHue;
    public static PaintCanvas instance;

    void Start()
    {
        if (instance != null)
        {
            Debug.LogError("Multiple PaintCanvas not implemented yet.");
        }
        instance = this;
        rectT = GetComponent<RectTransform>();
        var rawimg = GetComponent<RawImage>();
        //rt = rawimg.mainTexture as RenderTexture;
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        rawimg.texture = tex;

        EventTrigger trigger = GetComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Drag;
        entry.callback.AddListener((data) => { OnDraw((PointerEventData)data); });
        trigger.triggers.Add(entry);

        entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback.AddListener((data) => { OnDraw((PointerEventData)data); });
        trigger.triggers.Add(entry);
    }

    public void UpdateColor()
    {
        col = Color.HSVToRGB(colorPicker.hue, colorPicker.saturation, colorPicker.value);
    }

    public void ReceivePixelUpdate(ServerPixels pixels)
    {
        foreach(var pixel in pixels.pixels)
        {
            tex.SetPixel(pixel.x, pixel.y, new Color(pixel.color.red, pixel.color.green, pixel.color.blue, pixel.color.alpha));
        }
        tex.Apply();
    }

    public void OnDraw(PointerEventData eventData)
    {
        var pixels = PointerDataToPixelPos(eventData);
        tex.SetPixel(pixels.x, pixels.y, col);
        tex.Apply();
        ServerPixel pixel = new ServerPixel(pixels.x, pixels.y,new ServerColor(Convert.ToByte(col.r * 255), Convert.ToByte(col.g*255), Convert.ToByte(col.b*255), Convert.ToByte(col.a*255)) );
        ServerServiceHelper.SendPixelUpdate(pixel);

    }

    private Vector2Int PointerDataToPixelPos(PointerEventData eventData)
    {
        return RelativePosToPixelPos(PointerDataToRelativePos(eventData));
    }

    private Vector2Int RelativePosToPixelPos(Vector2 pos)
    {

        float xPercentage = pos.x / rectT.rect.width;
        float yPercentage = pos.y / rectT.rect.height;
        float pixelPosX = width * xPercentage;
        float pixelPosY = height * yPercentage;

        if (pixelPosX == width)
            pixelPosX--;

        if (pixelPosY == height)
            pixelPosY--;

        return new Vector2Int((int)pixelPosX, (int)pixelPosY);
    }

    private Vector2 PointerDataToRelativePos(PointerEventData eventData)
    {
        Vector2 result;
        Vector2 clickPosition = eventData.position;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectT, clickPosition, null, out result);
        result += rectT.rect.size - rectT.rect.position;

        return result;
    }

}
