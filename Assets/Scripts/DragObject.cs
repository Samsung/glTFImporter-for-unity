using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragObject : MonoBehaviour
{
    private Vector3 mOffset;
    private float mZcoord;
    private Vector3 position;
    private float width;
    private float height;
    private bool dragging = false;
    private Color activeColor = Color.gray;

    void Start()
    {
        GetComponent<MeshRenderer>().material.color = activeColor;
    }

    void Update()
    {
        ListenInput();

        /*
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == TouchPhase.Began)
            {

            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {

            }
            else if (touch.phase == TouchPhase.Moved)
            {

            }
        }
        */
    }

    void OnTouchedScreen(Touch touch)
    {
        mOffset = gameObject.transform.position - GetTouchWorldPos(touch);
    }

    private Vector3 GetTouchWorldPos(Touch touch)
    {
        Vector2 touchPoint = touch.position;

        return Camera.main.ScreenToViewportPoint(touchPoint);
    }

    void OnTouchedMouse()
    {
        Debug.Log("OnTouchedMouse");
        mZcoord = Camera.main.WorldToScreenPoint(gameObject.transform.position).z;

        //Store offset = gmaeobject world pos - mouse world pos
        mOffset = gameObject.transform.position - GetMouseWorldPos();
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;

        mousePoint.z = mZcoord;

        return Camera.main.ScreenToViewportPoint(mousePoint);
    }

    private void OnMouseDrag()
    {
        transform.position = GetMouseWorldPos() + mOffset;
        GetComponent<MeshRenderer>().material.color = Color.red;
    }

    private void OnMouseUp()
    {
        GetComponent<MeshRenderer>().material.color = Color.gray;
    }

    private void ListenInput()
    {
        if (Input.touchCount > 0)
        {
            // In Android
            Touch touch = Input.GetTouch(0);       // only touch 0 is used
            if (touch.phase == TouchPhase.Began)
            {
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider == GetComponent<BoxCollider>() || hit.collider == GetComponent<SphereCollider>())
                    {
                        OnTouchedScreen(touch);
                        dragging = true;
                    }
                }
            } else if (touch.phase == TouchPhase.Ended)
            {
                dragging = false;
                GetComponent<MeshRenderer>().material.color = Color.gray;
            }

            if (dragging && touch.phase == TouchPhase.Moved)
            {
                transform.position = GetTouchWorldPos(touch) + mOffset;
                GetComponent<MeshRenderer>().material.color = Color.red;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            // In Unity Studio
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider == GetComponent<BoxCollider>() || hit.collider == GetComponent<SphereCollider>())
                    OnTouchedMouse();
            }
        }
    }
}