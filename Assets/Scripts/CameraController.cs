using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    Vector3 FirstPoint;
    Vector3 SecondPoint;
    private float xAngle = 0f;
    private float yAngle = 0f;
    float xAngleTemp;
    float yAngleTemp;

    public void BeginDrag(Vector2 a_FirstPoint)
    {
        FirstPoint = a_FirstPoint;
        xAngleTemp = xAngle;
        yAngleTemp = yAngle;
    }

    public void OnDrag(Vector2 a_SecondPoint)
    {
        SecondPoint = a_SecondPoint;
        xAngle = xAngleTemp + (SecondPoint.x - FirstPoint.x) * 180 / Screen.width;
        yAngle = yAngleTemp - (SecondPoint.y - FirstPoint.y) * 90 * 3f / Screen.height; // Y값 변화가 좀 느려서 3배 곱해줌.
        
        transform.rotation = Quaternion.Euler(yAngle, xAngle, 0.0f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        BeginDrag(eventData.position);        
    }

    public void OnDrag(PointerEventData eventData)
    {
        OnDrag(eventData.position);
    }
}