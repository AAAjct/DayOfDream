using UnityEngine;
using DayOfDream.Horror;
if(UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Capture in Edit Mode.");
var previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var houseScene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/AnonHouse.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(houseScene);
UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>()?.gameObject.SetActive(false);
var root=new GameObject("Bedroom validation camera",typeof(Camera));
var camera=root.GetComponent<Camera>();
camera.clearFlags=CameraClearFlags.SolidColor;
camera.backgroundColor=Color.black;
camera.enabled=false;
camera.transform.position=new Vector3(-3.6f,1.55f,3.75f);
camera.transform.LookAt(new Vector3(-5.7f,.53f,4.85f));
camera.fieldOfView=62;
var rt=new RenderTexture(1600,900,24);
var previous=RenderTexture.active;
try
{
 camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
 var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
 image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
 System.IO.File.WriteAllBytes("output/dream_upgrade/bedroom.png",image.EncodeToPNG());
 UnityEngine.Object.DestroyImmediate(image);
}
finally
{
 RenderTexture.active=previous;camera.targetTexture=null;
 UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(root);
 UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
 UnityEditor.SceneManagement.EditorSceneManager.CloseScene(houseScene,true);
}
return "Bedroom captured";
