using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using DayOfDream.Horror;
if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("Play mode required");
const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
object Field(object o, string n) => o.GetType().GetField(n,F).GetValue(o);
void Set(object o,string n,object v) => o.GetType().GetField(n,F).SetValue(o,v);
object Call(object o,string n,params object[] a) => o.GetType().GetMethod(n,F).Invoke(o,a);
var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
var house=UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
var inventory=player.GetComponent<LeftHandInventory>();
var sound=player.GetComponent<ExplorerAudio>();
var hunter=UnityEngine.Object.FindAnyObjectByType<AnonHunter>();
if(hunter!=null)hunter.enabled=false;
var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Keyboard||d is Mouse||d is Gamepad)).ToArray();
foreach(var d in devices)InputSystem.DisableDevice(d);
var keyboard=InputSystem.AddDevice<Keyboard>();
var background=InputSystem.settings.backgroundBehavior;
InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
void Keys(params Key[] k)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(k));
var results=new System.Collections.Generic.List<object>();
var errors=new System.Collections.Generic.List<string>();
void Check(string name,bool passed,object detail)=>results.Add(new{name,passed,detail});
Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};
Application.logMessageReceived+=log;
var items=UnityEngine.Object.FindObjectsByType<HouseObject>(FindObjectsInactive.Include);
var small=items.First(x=>Field(x,"kind").ToString()=="CabinetKey");
var exit=items.First(x=>Field(x,"kind").ToString()=="ExitKey");
var fuse=items.First(x=>Field(x,"kind").ToString()=="Fuse");
var bottle=items.First(x=>Field(x,"kind").ToString()=="Bottle");
var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Inventory validation floor";
ground.transform.position=new Vector3(50,-.25f,50);ground.transform.localScale=new Vector3(50,.5f,70);
var lamp = new GameObject("Validation light").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=2;lamp.transform.rotation=Quaternion.Euler(55,20,0);
int stage=0,frame=-1;float since=Time.time;Vector3 start,shortEnd;float shortVelocity=0,noiseBefore=0;int stepsBefore=0;
UnityEditor.EditorApplication.CallbackFunction tick=null;
void Next(){stage++;since=Time.time;}
void Cleanup()
{
 UnityEditor.EditorApplication.update-=tick;Application.logMessageReceived-=log;
 if(keyboard.added)InputSystem.RemoveDevice(keyboard);
 foreach(var d in devices)if(d.added)InputSystem.EnableDevice(d);
 InputSystem.settings.backgroundBehavior=background;
 if(lamp!=null) { if(UnityEditor.EditorApplication.isPlaying)UnityEngine.Object.Destroy(lamp.gameObject);else UnityEngine.Object.DestroyImmediate(lamp.gameObject); }
 if(ground!=null) { if(UnityEditor.EditorApplication.isPlaying) UnityEngine.Object.Destroy(ground); else UnityEngine.Object.DestroyImmediate(ground); }
 System.IO.Directory.CreateDirectory("output/inventory_audio");
 System.IO.File.WriteAllText("output/inventory_audio/checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{results,errors},Newtonsoft.Json.Formatting.Indented));
}
tick=()=>
{
 if(!UnityEditor.EditorApplication.isPlaying||player==null){errors.Add("Play mode interrupted");Cleanup();return;}
 if(frame==Time.frameCount)return;frame=Time.frameCount;
 float dt=Time.time-since;
 if(stage<9 || (stage>11 && stage!=14)) player.SetPaused(false);
 try
 {
  switch(stage)
  {
   case 0:
    if((bool)Field(player,"<IsWaking>k__BackingField"))return;
    Set(house,"grace",1000f);player.SetPaused(false);
    Call(player,"Warp",new Vector3(50,.04f,30),Quaternion.identity,null);
    Call(house,"Use",small);Call(house,"Use",exit);Call(house,"Use",fuse);Call(house,"Use",bottle);
    Check("All gameplay audio imported",new[]{"Walk","Run","Throw","Pickup","ItemImpact","BottleBreak","ExecutionLaugh"}.All(x=>Resources.Load<AudioClip>("GameplayAudio/"+x)!=null),"7 clips");
    Keys(Key.Digit2);Next();break;
   case 1:
    if(dt<.2f)return;Keys();
    Check("2 selects exit key in left hand",exit.transform.IsChildOf(player.transform)&&exit.gameObject.activeInHierarchy,exit.transform.parent.name);
    Keys(Key.Digit3);Next();break;
   case 2:
    if(dt<.2f)return;Keys();
    Check("3 selects fuse and stows key",fuse.gameObject.activeInHierarchy&&!exit.gameObject.activeSelf,fuse.transform.parent.name);
    Keys(Key.Digit4);Next();break;
   case 3:
    if(dt<.2f)return;Keys();
    Check("4 selects bottle",bottle.transform.IsChildOf(player.transform)&&bottle.gameObject.activeInHierarchy,bottle.transform.parent.name);
    Keys(Key.Digit1);Next();break;
   case 4:
    if(dt<.2f)return;Keys();
    Check("1 selects small key",small.transform.IsChildOf(player.transform)&&small.gameObject.activeInHierarchy,small.transform.parent.name);
    Keys(Key.Q);Next();break;
   case 5:
    if(dt<.07f)return;Keys();Next();break;
   case 6:
    if(dt<.08f)return;
    var body=small.GetComponent<Rigidbody>();shortVelocity=body==null?0:body.linearVelocity.z;start=small.transform.position;
    Check("Tap Q releases physical item",body!=null&&!body.isKinematic&&Call(house,"GetSlot",0)==null,shortVelocity);
    Next();break;
   case 7:
    if(dt<1.5f)return;shortEnd=small.transform.position;
    Call(house,"Use",small);Keys(Key.Q);Next();break;
   case 8:
    if(dt<1.35f)return;
    Check("Hold Q reaches full charge with trajectory",(float)Field(inventory,"charge")>.99f&&((LineRenderer)Field(inventory,"arc")).enabled,Field(inventory,"charge"));
    ScreenCapture.CaptureScreenshot("output/inventory_audio/charged.png");Next();break;
   case 9:
    Keys();
    if(dt<.15f)return;
    var longBody=small.GetComponent<Rigidbody>();
    Check("Charged throw has much higher forward velocity",longBody.linearVelocity.z>shortVelocity*5,longBody.linearVelocity.ToString());
    player.SetPaused(true);Next();break;
   case 10:
    if(dt<.15f)return;start=small.transform.position;Next();break;
   case 11:
    if(dt<.3f)return;
    Check("Pause freezes thrown item",Vector3.Distance(start,small.transform.position)<.001f,small.transform.position.ToString());
    player.SetPaused(false);Next();break;
   case 12:
    if(dt<2f)return;
    Check("Thrown key lands on solid floor",small.transform.position.y > -.1f && small.transform.position.y < .4f,small.transform.position.ToString());
    Check("Charged throw travels farther",small.transform.position.z>shortEnd.z+4,new{shortZ=shortEnd.z,longZ=small.transform.position.z});
    Call(house,"Use",small);Keys(Key.Q);Next();break;
   case 13:
    if(dt<.5f)return;player.SetPaused(true);Keys();Next();break;
   case 14:
    if(dt<.15f)return;player.SetPaused(false);Next();break;
   case 15:
    if(dt<.2f)return;
    Check("Pause cancels charge without dropping",Call(house,"GetSlot",0)!=null&&!(bool)Field(inventory,"charging"),Field(inventory,"charge"));
    noiseBefore=(float)Field(house,"nextNoise");stepsBefore=(int)Field(sound,"<FootstepsPlayed>k__BackingField");Keys(Key.W);Next();break;
   case 16:
    if(dt<1.4f)return;
    Check("Walking emits footsteps",(int)Field(sound,"<FootstepsPlayed>k__BackingField")>stepsBefore,new { speed=player.MovementSpeed, count=Field(sound,"<FootstepsPlayed>k__BackingField"),grounded=player.IsGrounded });
    Check("Walking emits no enemy hearing event",(float)Field(house,"nextNoise")==noiseBefore,Field(house,"nextNoise"));
    Keys(Key.W,Key.LeftShift);Next();break;
   case 17:
    if(dt<.7f)return;
    Check("Running moves faster",player.MovementSpeed>3.3f,player.MovementSpeed);
    Keys();
    var thrownBottle=(HouseObject)Call(house,"ReleaseSlot",3);
    Call(thrownBottle,"Launch",player.transform.position+Vector3.up+player.transform.forward*.6f,player.transform.forward*2,house);
    Next();break;
   case 18:
    if(dt<1.4f)return;
    Check("Bottle collides and breaks",bottle==null || !bottle.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),"bottle destroyed or hidden after impact");
    Call(house,"BeginExecution");Next();break;
   case 19:
    if(dt<.15f)return;
    Check("Execution plays supplied laugh",((AudioSource)Field(sound,"effects")).isPlaying,((AudioClip)Field(sound,"execution")).name);
    Call(house,"FinishExecution");
    Check("Death drops all four objective slots",Enumerable.Range(0,4).All(i=>Call(house,"GetSlot",i)==null),"slots cleared");
    Check("No runtime errors",errors.Count==0,errors.ToArray());Cleanup();break;
  }
 }
 catch(Exception e){errors.Add(e.ToString());Cleanup();}
};
System.IO.Directory.CreateDirectory("output/inventory_audio");
UnityEditor.EditorApplication.update+=tick;
return new{started=true};
