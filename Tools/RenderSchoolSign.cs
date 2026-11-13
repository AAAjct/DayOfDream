var source=UnityEngine.Camera.main;
var ob=new UnityEngine.GameObject("Temporary Sign Validation Camera");
var camera=ob.AddComponent<UnityEngine.Camera>();camera.CopyFrom(source);
camera.transform.SetPositionAndRotation(new UnityEngine.Vector3(0,1.8f,5),UnityEngine.Quaternion.Euler(-25,180,0));
ob.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing=true;
var lamp=ob.AddComponent<UnityEngine.Light>();lamp.type=UnityEngine.LightType.Spot;lamp.intensity=240;lamp.range=35;lamp.spotAngle=52;lamp.color=new UnityEngine.Color(1,.95f,.8f);
var rt=UnityEngine.RenderTexture.GetTemporary(1280,720,24);var previous=UnityEngine.RenderTexture.active;
var image=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);
try
{
    camera.targetTexture=rt;camera.Render();UnityEngine.RenderTexture.active=rt;
    image.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);image.Apply();
    System.IO.File.WriteAllBytes("Docs/Validation/eqi_school_sign.png",image.EncodeToPNG());
}
finally
{
    UnityEngine.RenderTexture.active=previous;UnityEngine.RenderTexture.ReleaseTemporary(rt);
    UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(ob);
}
return "Rendered exported EQI school sign without changing the player pose.";
