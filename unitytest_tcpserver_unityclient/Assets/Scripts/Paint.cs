﻿using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Paint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    //public RenderTexture rt;
    public Texture2D tex;
    public RectTransform rectT;
    public Color col;
    public int width = 64;
    public int height = 64;

    void Start()
    {
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
    }

    void Update()
    {

    }

    public void OnDraw(PointerEventData eventData)
    {
        var pixels = PointerDataToPixelPos(eventData);
        tex.SetPixel(pixels.x, pixels.y, Color.red);
        tex.Apply();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
      
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var pixels = PointerDataToPixelPos(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }

    private Vector2Int PointerDataToPixelPos(PointerEventData eventData)
    {
        return RelativePosToPixelPos(PointerDataToRelativePos(eventData));
    }

    private Vector2Int RelativePosToPixelPos(Vector2 pos)
    {

        float xPercentage = pos.x / rectT.rect.width;
        float yPercentage = pos.y / rectT.rect.height;
        float pixelPosX = tex.width * xPercentage;
        float pixelPosY = tex.height * yPercentage;

        if (pixelPosX == tex.width)
            pixelPosX--;

        if (pixelPosY == tex.height)
            pixelPosY--;

        return new Vector2Int((int)pixelPosX, (int)pixelPosY);
    }

    private Vector2 PointerDataToRelativePos(PointerEventData eventData)
    {
        Vector2 result;
        Vector2 clickPosition = eventData.position;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectT, clickPosition, null, out result);
        result += rectT.rect.size / 2;

        return result;
    }

}