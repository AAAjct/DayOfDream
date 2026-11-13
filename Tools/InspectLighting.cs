var player = UnityEngine.Object.FindAnyObjectByType<DayOfDream.Horror.FirstPersonExplorer>();
player.SetPaused(true);
var camera = player.GetComponentInChildren<UnityEngine.Camera>();
var beam = UnityEngine.GameObject.Find("Flashlight Beam").GetComponent<UnityEngine.Light>();
var motor = player.GetComponent<UnityEngine.CharacterController>();
motor.enabled=false;player.transform.SetPositionAndRotation(new UnityEngine.Vector3(0,.18f,-9),UnityEngine.Quaternion.Euler(0,180,0));motor.enabled=true;
camera.transform.localPosition=new UnityEngine.Vector3(0,1.6f,.055f);camera.transform.localRotation=UnityEngine.Quaternion.identity;
UnityEngine.Physics.SyncTransforms();
System.IO.Directory.CreateDirectory("Docs/Validation");
double Capture(string name)
{
    var rt=UnityEngine.RenderTexture.GetTemporary(960,540,24);
    var previous=camera.targetTexture;var active=UnityEngine.RenderTexture.active;
    var tex=new UnityEngine.Texture2D(960,540,UnityEngine.TextureFormat.RGB24,false);
    try
    {
        camera.targetTexture=rt;camera.Render();UnityEngine.RenderTexture.active=rt;
        tex.ReadPixels(new UnityEngine.Rect(0,0,960,540),0,0);tex.Apply();
        System.IO.File.WriteAllBytes("Docs/Validation/"+name+".png",tex.EncodeToPNG());
        var pixels=tex.GetPixels();double sum=0;int count=0;
        for(int y=160;y<380;y++)for(int x=300;x<660;x++){var c=pixels[y*960+x];sum+=(c.r*.2126+c.g*.7152+c.b*.0722);count++;}
        return sum/count;
    }
    finally{camera.targetTexture=previous;UnityEngine.RenderTexture.active=active;UnityEngine.RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
}
var shadows=beam.shadows;
beam.enabled=false;double off=Capture("flashlight_off");
beam.enabled=true;double on=Capture("flashlight_on");
beam.shadows=UnityEngine.LightShadows.None;double noShadow=Capture("flashlight_no_shadow");
beam.shadows=shadows;
return new {off,on,noShadow,ratio=on/System.Math.Max(.00001,off),beamPosition=beam.transform.position.ToString(),body=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(player.GetComponentsInChildren<UnityEngine.Renderer>(),r=>new{r.name,bounds=r.bounds.ToString()})),ambient=UnityEngine.RenderSettings.ambientProbe[0,0],reflection=UnityEngine.RenderSettings.reflectionIntensity};
