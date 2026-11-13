using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using DayOfDream.Horror;
if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("Play Mode required");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "AnonHouse")
{
    UnityEngine.SceneManagement.SceneManager.LoadScene("AnonHouse");
    await System.Threading.Tasks.Task.Delay(700);
}
var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
var cam=player.GetComponentInChildren<Camera>();
var dv=cam.GetComponent<HandheldCamcorder>();
var house=UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
var hunter=UnityEngine.Object.FindAnyObjectByType<AnonHunter>();
var hud=player.GetComponentInChildren<HorrorHud>();
var door=GameObject.Find("Bedroom Door").GetComponent<HingedDoor>();
var motor=player.GetComponent<CharacterController>();
const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
object Field(object o,string name)=>o.GetType().GetField(name,flags).GetValue(o);
void Set(object o,string name,object value)=>o.GetType().GetField(name,flags).SetValue(o,value);
object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,flags).Invoke(o,args);
var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Keyboard||d is Mouse||d is Gamepad)).ToArray();
foreach(var d in devices) {InputSystem.ResetDevice(d);InputSystem.DisableDevice(d);}
var keyboard=InputSystem.AddDevice<Keyboard>();
var editorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
var background=InputSystem.settings.backgroundBehavior;
InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
var results=new System.Collections.Generic.List<object>();
var errors=new System.Collections.Generic.List<string>();
Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};
Application.logMessageReceived+=log;
var output=System.IO.Path.GetFullPath("output/bedroom_upgrade");
void Check(string name,bool passed,object detail)=>results.Add(new{name,passed,detail});
void Shot(string name)=>ScreenCapture.CaptureScreenshot(output+"/"+name+".png");
KeyboardState pendingKeys = new KeyboardState();
Action inject = () => { if (InputState.currentUpdateType == InputUpdateType.Dynamic) InputSystem.QueueStateEvent(keyboard, pendingKeys); };
InputSystem.onBeforeUpdate += inject;
void Keys(params Key[] keys) => pendingKeys = new KeyboardState(keys);
void Aim(Vector3 point)
{
 var heading=point-cam.transform.position;
 float yaw=Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg;
 player.transform.rotation=Quaternion.Euler(0,yaw,0);
 Set(player,"pitch",-Mathf.Atan2(heading.y,new Vector2(heading.x,heading.z).magnitude)*Mathf.Rad2Deg);
}
if(door.IsOpen)door.Toggle();hunter.enabled=true;Call(dv,"ReturnToTable");Set(house,"<IsExecuting>k__BackingField",false);Set(house,"blackout",0f);Set(house,"day",1);Call(hunter,"ResetPatrol");
Call(player,"BeginWake",Field(house,"wakePillow"),Field(house,"wakeExit"));
player.SetPaused(false);Set(house,"grace",100f);Set(house,"nextSave",float.PositiveInfinity);
bool finished=false;
int stage=0,previous=-1;float since=Time.time;float initial=Time.time;bool patrolEntered=false;int firstFrame=Time.frameCount;
void Next(){stage++;since=Time.time;}
UnityEditor.EditorApplication.CallbackFunction tick=null;
void Cleanup()
{
 finished=true;
 UnityEditor.EditorApplication.update-=tick;Application.logMessageReceived-=log;InputSystem.onBeforeUpdate-=inject;
 if(keyboard.added)InputSystem.RemoveDevice(keyboard);
 foreach(var d in devices)if(d.added)InputSystem.EnableDevice(d);
 InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorBehavior;
}
tick=()=>
{
 if(!UnityEditor.EditorApplication.isPlaying||player==null){Cleanup();return;}
 if(previous==Time.frameCount)return;previous=Time.frameCount;
 player.SetPaused(false);
 float elapsed=Time.time-since;
 try
 {
 switch(stage)
 {
 case 0:
  if(elapsed<1.1f)return;
  Check("First morning starts lying on bed",!motor.enabled&&cam.transform.position.y<.9f,cam.transform.position.ToString());
  var eyelids=hud.GetComponentsInChildren<UnityEngine.UI.Graphic>().FirstOrDefault(g=>g.name=="Eyelids");
  Check("Eyelid mesh renders",eyelids!=null&&eyelids.canvasRenderer!=null,eyelids==null?"missing":eyelids.canvasRenderer.ToString());
  Shot("01_waking_blurred");Next();break;
 case 1:
  if((bool)typeof(FirstPersonExplorer).GetProperty("IsWaking",flags).GetValue(player))return;
  Check("Wake ends standing with control",motor.enabled&&cam.transform.position.y>1.5f,new {eye=cam.transform.position.ToString(),frames=Time.frameCount-firstFrame});
  Check("Bed length increased",GameObject.Find("Bed").GetComponent<BoxCollider>().bounds.size.z>2.7f,GameObject.Find("Bed").GetComponent<BoxCollider>().bounds.size.ToString());
  var slots=hud.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x=>x.name=="Inventory Slot").ToArray();
  Check("Four larger square slots",slots.Length==4&&slots.All(x=>x.rectTransform.sizeDelta==new Vector2(104,104)),slots.Select(x=>x.rectTransform.sizeDelta.ToString()).ToArray());
  Check("Bedroom door initially closed",!door.IsOpen,door.IsOpen);
  Aim(GameObject.Find("DV Pickup").transform.position+Vector3.up*.015f);
  Next();break;
 case 2:
  if(elapsed<.25f)return;
  Shot("02_bedside_dv");
  var prompt=(TMPro.TMP_Text)Field(hud,"prompt");
  Check("No aiming hint text",prompt.text=="",prompt.text);
  Check("Pickup uses same deluxe model",((Transform)Field(dv,"model")).GetComponentsInChildren<Renderer>(true).Length>=6,
   ((Transform)Field(dv,"model")).childCount);
  Keys(Key.E);Next();break;
 case 3:
  if(elapsed<.15f)return;Keys();
  Check("DV picked up with E",dv.IsCarried,dv.IsCarried);
  Next();break;
 case 4:
  if(elapsed<.6f)return;
  Shot("03_deluxe_dv_carried");
  var labels=hud.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x=>x.name=="Item image").ToArray();
  Check("DV occupies no inventory slot",labels.All(x=>x.sprite==null||!x.sprite.name.Contains("DV"))&&labels.Length==4,labels.Select(x=>new{sprite=x.sprite==null?"none":x.sprite.name,x.enabled}).ToArray());
  dv.ToggleRaised();Next();break;
 case 5:
  if(elapsed<.7f)return;dv.ToggleNightVision();Next();break;
 case 6:
  if(elapsed<.7f)return;
  Check("Night vision still works",dv.InfraredActive,dv.InfraredActive);
  Shot("04_night_vision");
  dv.Lower();
  Call(player,"Warp",new Vector3(-2.65f,.04f,3.42f),Quaternion.Euler(0,90,0),null);
  Aim(new Vector3(-1.43f,1.22f,3.42f));
  Next();break;
 case 7:
  if(elapsed<.4f)return;Keys(Key.E);Next();break;
 case 8:
  if(elapsed<.2f)return;Keys();
  Check("Bedroom door opens with E",door.IsOpen,door.IsOpen);
  Shot("05_bedroom_door");
  if(door.IsOpen)door.Toggle();
  Call(player,"Warp",new Vector3(-4.58f,.04f,5.10f),Quaternion.Euler(0,90,0),null);
  Set(player,"pitch",0f);Next();break;
 case 9:
  var p=hunter.transform.position;
  patrolEntered|=p.x< -1.65f&&p.z>.35f&&p.y>-.5f;
  if(elapsed<3)return;
  Check("Anon never enters bedroom during patrol",!patrolEntered,new{patrolEntered,position=p.ToString(),seconds=elapsed});
  Set(house,"grace",0f);
  Call(player,"Warp",new Vector3(-.55f,.04f,-3.43f),Quaternion.Euler(0,90,0),null);
  var agent=hunter.GetComponent<UnityEngine.AI.NavMeshAgent>();
  agent.Warp(new Vector3(.55f,0,-3.43f));agent.ResetPath();hunter.transform.rotation=Quaternion.Euler(0,270,0);
  Next();break;
 case 10:
  if(!(bool)typeof(AnonHouse).GetProperty("IsExecuting",flags).GetValue(house))
  {if(elapsed>3)throw new Exception("Execution did not trigger");return;}
  Next();break;
 case 11:
  if(elapsed<.7f)return;
  Check("Victim is on floor",cam.transform.position.y<.5f,cam.transform.position.ToString());
  Shot("06_execution_fallen");Next();break;
 case 12:
  if(elapsed<.77f)return;
  Shot("07_two_handed_overhead");
  var club=hunter.GetComponentsInChildren<Transform>().First(x=>x.name=="Wooden Club");
  Check("Club raised above victim",club.position.y>1.5f,club.position.ToString());
  Next();break;
 case 13:
  if(elapsed<.9f)return;
  var eyeGraphic=hud.GetComponentsInChildren<UnityEngine.UI.Graphic>().First(g=>g.name=="Eyelids");
  Check("Impact closes eyes",(float)Field(eyeGraphic,"opening")<.01f,Field(eyeGraphic,"opening"));
  Shot("08_unconscious");Next();break;
 case 14:
  if(!(bool)typeof(FirstPersonExplorer).GetProperty("IsWaking",flags).GetValue(player))
  {if(elapsed>5)throw new Exception("Second wake did not begin");return;}
  Check("Second day begins on same bed",(int)Field(house,"day")==2&&!motor.enabled,new{day=Field(house,"day"),eye=cam.transform.position.ToString()});
  Next();break;
 case 15:
  if(elapsed<1.2f)return;Shot("09_day_two_waking");Next();break;
 case 16:
  if((bool)typeof(FirstPersonExplorer).GetProperty("IsWaking",flags).GetValue(player))return;
  Check("Second morning restores standing controls",motor.enabled&&cam.transform.position.y>1.5f,cam.transform.position.ToString());
  Check("DV remains separate carried equipment",dv.IsCarried&&!((CamcorderPickup)Field(dv,"pickup")).gameObject.activeSelf,dv.IsCarried);
  Check("No runtime errors",errors.Count==0,errors.ToArray());
  System.IO.File.WriteAllText(output+"/checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{results,errors,duration=Time.time-initial},Newtonsoft.Json.Formatting.Indented));
  Cleanup();break;
 }
 }
 catch(Exception e)
 {
  errors.Add(e.ToString());System.IO.File.WriteAllText(output+"/checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{results,errors},Newtonsoft.Json.Formatting.Indented));Cleanup();
 }
};
UnityEditor.EditorApplication.update+=tick;
while(!finished) await System.Threading.Tasks.Task.Delay(40);
if(stage<16 && errors.Count==0) errors.Add("Validation interrupted by an external scene or Play mode change at stage "+stage);
System.IO.File.WriteAllText(output+"/checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{results,errors,duration=UnityEngine.Time.time-initial},Newtonsoft.Json.Formatting.Indented));
return new{completed=stage>=16,checks=results.Count,errors};
