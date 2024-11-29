using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyButtonMoveEvent : MonoBehaviour
{
    public Animator anim;
    public GameObject GO;

    void Start()
    {
        if (GameObject.Find("GLTFComponent"))
        {
            GO = GameObject.Find("GLTFComponent");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (anim == null)
        {
            GameObject o = GameObject.Find("rig_GRP");
            if (o != null)
            {
                anim = o.GetComponent<Animator>();
            }

        }
        else
        {
            if (Input.GetKey(KeyCode.RightArrow))
            {
                Vector3 position = GO.transform.position;
                position.x += 0.1f;
                GO.transform.position = position;

                GO.transform.rotation = Quaternion.Euler(0, -90, 0);
                anim.SetBool("walk", true);
            }
            else if (Input.GetKey(KeyCode.LeftArrow))
            {
                Vector3 position = GO.transform.position;
                position.x -= 0.1f;
                GO.transform.position = position;

                GO.transform.rotation = Quaternion.Euler(0, 90, 0);
                anim.SetBool("walk", true);
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                Vector3 position = GO.transform.position;
                position.z += 0.1f;
                GO.transform.position = position;

                GO.transform.rotation = Quaternion.Euler(0, 180, 0);
                anim.SetBool("walk", true);
            }
            else if (Input.GetKey(KeyCode.UpArrow))
            {
                Vector3 position = GO.transform.position;
                position.z -= 0.1f;
                GO.transform.position = position;

                GO.transform.rotation = Quaternion.Euler(0, 0, 0);
                anim.SetBool("walk", true);
            }
            else if (Input.GetKey(KeyCode.Space))
            {
                //transform.rotation = Quaternion.Euler(0, 0, 0);
                anim.SetBool("jump", true);
            }
            else
            {
                anim.SetBool("walk", false);
                anim.SetBool("jump", false);
            }
        }
    }
}