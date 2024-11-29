using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotation : MonoBehaviour
{
    private float mPreTouchPos = 0;
    private Vector2 dir = Vector2.zero;
    private Vector2 pos = Vector2.zero;

    void Start()
    {

    }

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_WEBGL || UNITY_EDITOR_OSX)
    float rotSpeed = 150;
    float pinchSpeed = 100;
#elif (UNITY_ANDROID || UNITY_IOS)
    float rotSpeed = 10;
    float pinchSpeed = 1;
#else
    float rotSpeed = 30;
    float pinchSpeed = 5;
#endif

    void OnMouseDrag()
    {
        float rotX = Input.GetAxis("Mouse X") * rotSpeed * Mathf.Deg2Rad;
        float rotY = Input.GetAxis("Mouse Y") * rotSpeed * Mathf.Deg2Rad;

        gameObject.transform.Rotate(Vector3.up, -rotX);
        gameObject.transform.Rotate(Vector3.right, rotY);
    }

    bool zoom()
    {
        if (Input.touchCount != 2) return false;

        Vector2 pos1 = Input.GetTouch(0).position;
        Vector2 pos2 = Input.GetTouch(1).position;

        Vector2 delta1 = pos1 - Input.GetTouch(0).deltaPosition;
        Vector2 delta2 = pos2 - Input.GetTouch(1).deltaPosition;

        float f1 = (pos1 - pos2).magnitude;
        float f2 = (delta1 - delta2).magnitude;
        float dist = f1 - f2;

        Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView - dist * pinchSpeed, 10, 90);
        return true;
    }

    // Update is called once per frame
    void Update()
    {

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        if (Input.GetMouseButton(0))
        {
            float rotX = (Input.GetAxis("Mouse X") - mPreTouchPos) * rotSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, -rotX);
        }

        float f = Input.GetAxis("Mouse ScrollWheel");

        Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView - f * pinchSpeed, 10, 90);


#elif UNITY_ANDROID
        if(zoom()){
        
        }
        else{
            if (Input.touchCount > 0)
            {
                if (Input.GetTouch(0).phase == TouchPhase.Began)
                {
                    pos = Input.GetTouch(0).position;
                }

                if (Input.GetTouch(0).phase == TouchPhase.Moved)
                {
                    pos = Input.GetTouch(0).deltaPosition;
                    dir.y = -pos.x;
                    dir.x = pos.y;
            
                    transform.Rotate(Vector3.up, dir.y * rotSpeed * Time.deltaTime);
                    pos = Input.GetTouch(0).position;
                }
            }
        }  
#endif
    }
}