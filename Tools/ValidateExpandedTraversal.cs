using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using DayOfDream.Horror;
var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
var house=UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
var hunter=UnityEngine.Object.FindAnyObjectByType<AnonHunter>();
var cam=player.GetComponentInChildren<Camera>();
var motor=player.GetComponent<CharacterController>();
const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Keyboard||d is Mouse||d is Gamepad)).ToArray();
foreach(var d in devices)InputSystem.DisableDevice(d);
var keys=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
var editorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
var results=new System.Collections.Generic.List<object>();var errors=new System.Collections.Generic.List<string>();
void Check(string name,bool passed,object detail)=>results.Add(new{name,passed,detail});
void Keys(params Key[] state)=>InputSystem.QueueStateEvent(keys,new KeyboardState(state));
void Warp(Vector3 p,float yaw){Keys();Call(player,"Warp",p,Quaternion.Euler(0,yaw,0),null);Set(player,"pitch",0f);player.SetPaused(false);}
string dir=System.IO.Path.GetFullPath("output/bedroom_upgrade");
void Shot(string n)=>ScreenCapture.CaptureScreenshot(dir+"/"+n+".png");
Application.LogCallback logs=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};
Application.logMessageReceived+=logs;
Set(house,"grace",1000f);Set(house,"nextSave",float.PositiveInfinity);Call(hunter,"ResetPatrol");hunter.GetComponent<NavMeshAgent>().isStopped=true;hunter.enabled=false;
Set(player,"<IsWaking>k__BackingField",false); // Replaced below with auto-property backing field.
int stage=0,previous=-1,waypoint=0;float since=Time.time;float first=Time.time;
Vector3[] route={new(11.55f,0,-5.5f),new(14.65f,1.65f,-5.5f),new(14.65f,3.3f,1.1f),new(11.55f,3.3f,1.1f),
 new(11.55f,3.3f,-5.5f),new(14.65f,4.95f,-5.5f),new(14.65f,6.6f,1.1f)};
var bedroomDoor=GameObject.Find("Bedroom Door").GetComponent<HingedDoor>();
void Next(){stage++;since=Time.time;}
UnityEditor.EditorApplication.CallbackFunction tick=null;
void Done()
{
 Keys();UnityEditor.EditorApplication.update-=tick;Application.logMessageReceived-=logs;
 if(keys.added)InputSystem.RemoveDevice(keys);if(mouse.added)InputSystem.RemoveDevice(mouse);
 foreach(var d in devices)if(d.added)InputSystem.EnableDevice(d);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorBehavior;
 hunter.enabled=true;Call(hunter,"ResetPatrol");
 System.IO.File.WriteAllText(dir+"/traversal_stealth_checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{results,errors,duration=Time.time-first},Newtonsoft.Json.Formatting.Indented));
}
Warp(new(-3.5f,.04f,3.5f),180);
Call(hunter,"ResetPatrol"); // disabled hunter intentionally remains in place for acoustic probes
hunter.GetComponent<NavMeshAgent>().Warp(new Vector3(-.5f,0,3.43f));hunter.transform.rotation=Quaternion.Euler(0,270,0);
Set(hunter,"alertUntil",0f);Set(hunter,"bedroomAccessUntil",0f);Set(house,"grace",0f);
tick=()=>
{
 if(!UnityEditor.EditorApplication.isPlaying||player==null){Done();return;}
 if(previous==Time.frameCount)return;previous=Time.frameCount;player.SetPaused(false);
 float elapsed=Time.time-since;
 try
 {
 switch(stage)
 {
 case 0:
  Keys(Key.W);if(elapsed<.6f)return;Keys();
  Check("Walking produces no audible detection",(float)Get(hunter,"alertUntil")==0,(float)Get(hunter,"alertUntil"));
  var audio=player.GetComponent<ExplorerAudio>();var footsteps=(AudioSource)Get(audio,"steps");
  Check("Walking has no footstep audio",!footsteps.isPlaying,footsteps.isPlaying);
  Keys(Key.C);Next();break;
 case 1:
  if(elapsed<.12f)return;Keys();Next();break;
 case 2:
  if(elapsed<.3f)return;
  Check("C toggles crouch after release",player.IsCrouched,player.IsCrouched);Keys(Key.W);Next();break;
 case 3:
  if(elapsed<.55f)return;Keys();
  Check("Crouch walking is silent",(float)Get(hunter,"alertUntil")==0&&!((AudioSource)Get(player.GetComponent<ExplorerAudio>(),"steps")).isPlaying,Get(hunter,"alertUntil"));
  Keys(Key.C);Next();break;
 case 4:
  if(elapsed<.12f)return;Keys();Next();break;
 case 5:
  if(elapsed<.3f)return;
  Check("Second C toggles standing",!player.IsCrouched,player.IsCrouched);
  Warp(new(-3.5f,.04f,3.43f),90);Keys(Key.W,Key.LeftShift);Next();break;
 case 6:
  if(elapsed<.35f)return;Keys();
  Check("Running creates a small audible cue",(float)Get(hunter,"bedroomAccessUntil")>Time.time,Get(hunter,"bedroomAccessUntil"));
  Set(hunter,"bedroomAccessUntil",0f);Set(hunter,"alertUntil",0f);
  Check("Silent bedroom blocks visual targeting",!(bool)typeof(AnonHunter).GetProperty("CanSeePlayer",F).GetValue(hunter),player.transform.position.ToString());
  Set(house,"grace",1000f);Warp(new(11.55f,.05f,1.1f),180);Next();break;
 case 7:
  if(waypoint>=route.Length)
  {
   Keys();Check("Walked continuously from ground to attic",player.transform.position.y>6.45f,player.transform.position.ToString());
   Shot("10_attic_arrival");Next();break;
  }
  var delta=route[waypoint]-player.transform.position;delta.y=0;
  if(delta.magnitude<.18f){waypoint++;since=Time.time;return;}
  player.transform.rotation=Quaternion.LookRotation(delta);Keys(Key.W);
  if(elapsed>9)throw new Exception("Stair traversal stuck at waypoint "+waypoint+" position "+player.transform.position);
  break;
 case 8:
  if(elapsed<.35f)return;
  Warp(new(-2.75f,.04f,3.43f),90);
  if(bedroomDoor.IsOpen)bedroomDoor.Toggle();Next();break;
 case 9:
  if(elapsed<.9f)return;
  bedroomDoor.Toggle(player.transform.position);Next();break;
 case 10:
  if(elapsed<.9f)return;
  float a=(float)Get(bedroomDoor,"swingAngle");
  Check("Door opens away from inside player",a!=0,a);
  bedroomDoor.Toggle();Next();break;
 case 11:
  if(elapsed<.9f)return;
  bedroomDoor.Toggle(new Vector3(-.4f,1.2f,3.43f));Next();break;
 case 12:
  if(elapsed<.9f)return;
  float b=(float)Get(bedroomDoor,"swingAngle");
  Check("Door reverses swing from outside",b<0,b);
  bedroomDoor.Toggle();
  var dv=cam.GetComponent<HandheldCamcorder>();Call(dv,"ReturnToTable");
  Warp(new(-4.58f,.04f,5.10f),15);
  Set(player,"pitch",41f);Next();break;
 case 13:
  if(elapsed<.4f)return;
  Shot("11_lit_bedroom_dv");
  Check("No headboard remains",!GameObject.Find("Bed").transform.Find("Headboard").gameObject.activeSelf,false);
  var model=(Transform)Get(cam.GetComponent<HandheldCamcorder>(),"model");
  Check("DV uses Lit world light layers",model.GetComponentsInChildren<Renderer>(true).All(r=>(r.renderingLayerMask&1)!=0&&r.sharedMaterial.shader.name=="Universal Render Pipeline/Lit"),model.childCount);
  Check("No artificial hand softbox",cam.transform.Find("DV Softbox")==null,cam.transform.Find("DV Softbox")==null);
  player.enabled=false;motor.enabled=false;cam.transform.SetPositionAndRotation(new(26,14,24),Quaternion.LookRotation(new Vector3(3,4,0)-new Vector3(26,14,24)));
  Call(cam.GetComponent<PlayerConsciousness>(),"Set",0f,1f);Next();break;
 case 14:
  if(elapsed<.3f)return;Shot("12_four_storey_house_and_courtyard");Next();break;
 case 15:
  if(elapsed<.3f)return;player.enabled=true;Warp(new(-4.58f,.04f,5.10f),90);
  Check("No runtime errors",errors.Count==0,errors.ToArray());Done();break;
 }
 }
 catch(Exception e){errors.Add(e.ToString());Done();}
};
UnityEditor.EditorApplication.update+=tick;
return "Traversal and stealth validation started";

