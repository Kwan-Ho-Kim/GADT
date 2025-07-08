using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WindowManager : MonoBehaviour, IDragHandler, IScrollHandler
{
    RectTransform rawImageTransform;

    Canvas canvas;

    private void Start()
    {
        rawImageTransform = GetComponent<RectTransform>();
        canvas = FindObjectOfType<Canvas>();
    }

    void OnEnable()
    {
        GetComponent<RawImage>().color = new Color(1, 1, 1, 1);
    }

    // �巡���� �� ȣ���
    public void OnDrag(PointerEventData eventData)
    {
        rawImageTransform.anchoredPosition += (eventData.delta / canvas.transform.lossyScale);
    }

    // ��ũ���� �� ȣ���
    public void OnScroll(PointerEventData eventData)
    {
        float scaleFactor = 1.0f + eventData.scrollDelta.y * 0.1f;
        rawImageTransform.localScale *= scaleFactor;
    }
}