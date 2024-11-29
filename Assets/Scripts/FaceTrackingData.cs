using UnityEngine;

public class FaceTrackingData
{
    public float[] morphWeight = new float[68];       // result of expression weights with the patch expression weights
    public float headAngle;             // the roll yaw pitch angel of the head, radian
    public float[] headAngleAxis = new float[3];      //
    public float leftEyeAngle;          // the roll yaw pitch angel of the left eye, radian
    public float[] leftEyeAngleAxis = new float[3];   //
    public float rightEyeAngle;         // the roll yaw pitch angel of the right eye, radian
    public float[] rightEyeAngleAxis = new float[3];  //

    public FaceTrackingData(AndroidJavaObject javaFtDataObj)
    {
        this.morphWeight = javaFtDataObj.Get<float[]>("morphWeight");
        this.headAngle = javaFtDataObj.Get<float>("headAngle");
    }
}