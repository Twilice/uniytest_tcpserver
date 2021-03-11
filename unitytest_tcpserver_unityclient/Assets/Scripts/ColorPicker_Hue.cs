using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;

public class ColorPicker_Hue : MonoBehaviour
{
    private RectTransform rectT;

    void Start()
    {
        rectT = GetComponent<RectTransform>();


        EventTrigger trigger = GetComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Drag;
        entry.callback.AddListener((data) => { OnChooseHue((PointerEventData)data); });
        trigger.triggers.Add(entry);

        entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback.AddListener((data) => { OnChooseHue((PointerEventData)data); });
        trigger.triggers.Add(entry);
    }

    public void OnChooseHue(PointerEventData eventData)
    {
        var newHue = RelativePosToHue(PointerDataToRelativePos(eventData));
        PaintCanvas.instance.colorPicker.hue = newHue;
        PaintCanvas.instance.colorPicker.UpdateShader();
        PaintCanvas.instance.UpdateColor();
    }

    private float RelativePosToHue(Vector2 pos)
    {
        float yPercentage = pos.y / rectT.rect.height;

        return yPercentage;
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
