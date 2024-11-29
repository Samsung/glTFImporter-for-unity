using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class BvhLoader
{

    private String Source;

    public Bvh mBvh;
    public AnimationClip Animation;

    [Obsolete]
    public BvhLoader(string path)
    {

        if (Application.platform == RuntimePlatform.Android) //Need to extract file from apk first
        {
            WWW reader = new WWW(path);
            while (!reader.isDone) { }
            Source = reader.text;
        }
        else
        {
            Source = File.ReadAllText(path, Encoding.UTF8);
        }
        mBvh = Parse(Source);

        CreateAnimationClip();

    }

    public Bvh Parse(string src)
    {
        var srcline = new StringReader(src);

        if (srcline.ReadLine() != "HIERARCHY") throw new Exception("Must be HIERARCHY");
        List<BvhNode> Nodes = new List<BvhNode>();

        if (!ParseNode(Nodes, srcline)) return null;

        if (srcline.ReadLine() != "MOTION") throw new Exception("Must be MOTION");

        var frameSplited = srcline.ReadLine().Split(':');
        if (frameSplited[0] != "Frames") throw new Exception("Must be Frames");
        int frames = int.Parse(frameSplited[1]);

        var frameTimeSplited = srcline.ReadLine().Split(':');
        if (frameTimeSplited[0] != "Frame Time") throw new Exception("Must be Frame Time");
        float frameTime = float.Parse(frameTimeSplited[1]);


        var bvh = new Bvh(Nodes, frames, frameTime);

        for (int i = 0; i < frames; ++i)
        {
            var line = srcline.ReadLine();
            var splited = line.Trim().Split().Where(x => !string.IsNullOrEmpty(x)).ToArray();

            for (int j = 0; j < bvh.Channels.Length; ++j)
                bvh.Channels[j][i] = float.Parse(splited[j]);

        }

        return bvh;

    }
    public bool ParseNode(List<BvhNode> nodes, StringReader srcline, BvhNode pareNode = null)
    {
        var oneline = srcline.ReadLine().Trim();
        var splited = oneline.Split();
        if (splited[0] == "}") return false;

        BvhNode node = null;
        switch (splited[0])
        {
            case "ROOT":
            case "JOINT":
                node = new BvhNode(splited[1], pareNode); nodes.Add(node);
                break;
            case "End": node = new BvhNode("End", null); ; break;
            default: break;
        }
        if (!srcline.ReadLine().Trim().Equals("{")) throw new Exception("Must be '{'  ");

        node.Parse(srcline);


        while (true)
        {
            if (!ParseNode(nodes, srcline, node)) break;
        }
        return true;
    }

    public class Bvh
    {
        public List<BvhNode> Nodes;
        public TimeSpan FrameTime;
        public int m_frames;
        public int channelCount;
        public float[][] Channels;

        public Bvh(List<BvhNode> nodes, int frames, float seconds)
        {
            this.Nodes = nodes;
            FrameTime = TimeSpan.FromSeconds(seconds);
            m_frames = frames;
            channelCount = 0;
            for (int i = 0; i < Nodes.Count; i++) channelCount += Nodes[i].Channels.Length;

            Channels = new float[channelCount][];
            for (int i = 0; i < channelCount; i++)
                Channels[i] = new float[frames];

        }

        public string GetPath(BvhNode node)
        {
            if (node == null) return "";

            string Path = GetPath(node.pareNode);

            return Path + (Path == "" ? node.Name : "/" + node.Name);

        }
    }

    public class BvhNode
    {
        public String Name;
        public float[] offset;
        public string[] Channels;
        public BvhNode pareNode;
        public BvhNode(string name, BvhNode pareNode) { this.Name = name; this.pareNode = pareNode; }
        public void Parse(StringReader r)
        {
            if (this.Name.Equals("End"))
            {
                r.ReadLine();
                return;
            }

            string line = r.ReadLine();
            var splited = line.Trim().Split();
            offset = new float[3] { float.Parse(splited[1]), float.Parse(splited[2]), float.Parse(splited[3]) };

            line = r.ReadLine();
            splited = line.Trim().Split();
            var count = int.Parse(splited[1]);
            Channels = new string[count];
            for (int i = 0; i < count; i++) Channels[i] = (string)splited[i + 2].Clone();

        }

        public Func<float, float, float, Quaternion> GetEulerToRotation()
        {
            return (x, y, z) =>
            {
                var xRot = Quaternion.Euler(x, 0, 0);
                var yRot = Quaternion.Euler(0, y, 0);
                var zRot = Quaternion.Euler(0, 0, z);

                var r = Quaternion.identity;
                for (int i = 0; i < this.Channels.Length; i++)
                {
                    switch (this.Channels[i])
                    {
                        case "Xrotation": r = r * xRot; break;
                        case "Yrotation": r = r * yRot; break;
                        case "Zrotation": r = r * zRot; break;
                        default: break;
                    }
                }
                return r;
            };
        }
    }

    public AnimationClip CreateAnimationClip()
    {


        var clip = new AnimationClip();
        clip.legacy = true;

        var curveList = new List<Curve>();


        for (int i = 0, resultCount = 0; i < mBvh.Nodes.Count; i++)
        {
            var node = mBvh.Nodes[i];
            var set = new Curve(node);
            curveList.Add(set);

            for (int j = 0; j < node.Channels.Length; j++, resultCount++)
            {
                var curve = mBvh.Channels[resultCount];
                switch (node.Channels[j])
                {
                    case "Xposition": set.positionX = curve; break;
                    case "Yposition": set.positionY = curve; break;
                    case "Zposition": set.positionZ = curve; break;

                    case "Xrotation": set.RotationX = curve; break;
                    case "Yrotation": set.RotationY = curve; break;
                    case "Zrotation": set.RotationZ = curve; break;
                    default: break;
                }
            }
        }
        for (int i = 0; i < curveList.Count; i++)
        {
            curveList[i].AddCurves(mBvh, clip);
        }

        clip.EnsureQuaternionContinuity();
        clip.name = "AnimationBody";
        clip.legacy = true;
        return clip;
    }

    class Curve
    {
        BvhNode Node;
        Func<float, float, float, Quaternion> EulerToRotation;
        public Curve(BvhNode node) { Node = node; }

        public float[] positionX;
        public float[] positionY;
        public float[] positionZ;
        public float[] RotationX;
        public float[] RotationY;
        public float[] RotationZ;

        public Quaternion GetRotation(int i)
        {
            if (EulerToRotation == null)
            {
                EulerToRotation = Node.GetEulerToRotation();
            }
            return EulerToRotation(
                RotationX[i],
                RotationY[i],
                RotationZ[i]
                );
        }
        public Quaternion ReverseX(Quaternion quaternion)
        {
            float angle;
            Vector3 axis;
            quaternion.ToAngleAxis(out angle, out axis);

            return Quaternion.AngleAxis(-angle, new Vector3(-axis.x, axis.y, axis.z));
        }

        private bool isJunior()
        {
            Transform RootNode = GameObject.Find("RootNode").transform;
            Transform charactor = RootNode.GetChild(0);
            if (charactor.name.Contains("junior"))
                return true;
            return false;
        }

        public void AddCurves(Bvh bvh, AnimationClip clip)
        {

            var relativePath = bvh.GetPath(this.Node);

            if (this.Node.Name.Equals("hips_JNT"))
            {
                //float scaling = isJunior() ? 0.0048f : 0.01f;
                float scaling = 0.01f;

                var posX = new AnimationCurve();
                var posY = new AnimationCurve();
                var posZ = new AnimationCurve();

                for (int i = 0; i < bvh.m_frames; ++i)
                {
                    var time = (float)(i * bvh.FrameTime.TotalSeconds);
                    posX.AddKey(time, positionX[i] * scaling);
                    posY.AddKey(time, positionY[i] * scaling);
                    posZ.AddKey(time, -positionZ[i] * scaling);
                }
                clip.SetCurve(relativePath, typeof(Transform), "localPosition.x", posX);
                clip.SetCurve(relativePath, typeof(Transform), "localPosition.y", posY);
                clip.SetCurve(relativePath, typeof(Transform), "localPosition.z", posZ);
            }


            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            var curveW = new AnimationCurve();





            for (int i = 0; i < bvh.m_frames; ++i)
            {
                var time = (float)(i * bvh.FrameTime.TotalSeconds);
                var q = ReverseX(GetRotation(i));

                curveX.AddKey(time, q.x);
                curveY.AddKey(time, -q.y);
                curveZ.AddKey(time, q.z);
                curveW.AddKey(time, -q.w);
            }
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.x", curveX);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.y", curveY);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.z", curveZ);
            clip.SetCurve(relativePath, typeof(Transform), "localRotation.w", curveW);


        }
    }
}
