using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorPicker : MonoBehaviour
{
    private RectTransform rectT;
    private Material mat;
    private int propertyId;
    public float hue;
    public float saturation;
    public float value;

    void Start()
    {
        rectT = GetComponent<RectTransform>();
        mat = GetComponent<Image>().material;
        propertyId = Shader.PropertyToID("_Hue");


        EventTrigger trigger = GetComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Drag;
        entry.callback.AddListener((data) => { OnChooseColor((PointerEventData)data); });
        trigger.triggers.Add(entry);

        entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback.AddListener((data) => { OnChooseColor((PointerEventData)data); });
        trigger.triggers.Add(entry);
    }

    public void OnChooseColor(PointerEventData eventData)
    {
        var sv = PointerDataToSaturationValue(eventData);
        saturation = sv.saturation;
        value = sv.value;
        UpdateShader();
        PaintCanvas.instance.UpdateColor();
    }

    public void UpdateShader()
    {
        mat.SetFloat(propertyId, hue);
    }

    private (float saturation, float value) PointerDataToSaturationValue(PointerEventData eventData)
    {
        return RelativePosToSaturationValue(PointerDataToRelativePos(eventData));
    }

    private (float saturation, float value) RelativePosToSaturationValue(Vector2 pos)
    {

        float xPercentage = pos.x / rectT.rect.width;
        float yPercentage = pos.y / rectT.rect.height;

        return (xPercentage, yPercentage);
    }

    private Vector2 PointerDataToRelativePos(PointerEventData eventData)
    {
        Vector2 result;
        Vector2 clickPosition = eventData.position;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectT, clickPosition, null, out result);
        result += rectT.rect.size + rectT.rect.position;

        return result;
    }
}
