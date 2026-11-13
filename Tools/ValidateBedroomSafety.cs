using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using DayOfDream.Horror;
const BindingFlags f=BindingFlags.Instance|BindingFlags.NonPublic;
var p=UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
var h=UnityEngine.Object.FindAnyObjectByType<AnonHunter>();
var house=UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
var door=GameObject.Find("Bedroom Door").GetComponent<HingedDoor>();
var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Keyboard||d is Mouse||d is Gamepad)).ToArray();
foreach(var d in devices){InputSystem.ResetDevice(d);InputSystem.DisableDevice(d);}
house.GetType().GetField("nextSave",f).SetValue(house,float.PositiveInfinity);
house.GetType().GetField("grace",f).SetValue(house,0f);
p.GetType().GetMethod("Warp",f).Invoke(p,new object[]{new Vector3(-4.58f,.04f,5.1f),Quaternion.Euler(0,90,0),null});
p.SetPaused(false);if(!door.IsOpen)door.Toggle();
h.enabled=true;h.GetType().GetMethod("ResetPatrol",f).Invoke(h,null);
var room=(Bounds)h.GetType().GetField("bedroom",f).GetValue(h);
bool entered=false;int frames=0;
UnityEditor.EditorApplication.CallbackFunction watch=()=>{
 if(p==null||h==null)return;p.SetPaused(false);entered|=room.Contains(h.transform.position);frames++;
};
UnityEditor.EditorApplication.update+=watch;
await System.Threading.Tasks.Task.Delay(25000);
UnityEditor.EditorApplication.update-=watch;
bool quietSafe=!entered;
h.GetComponent<NavMeshAgent>().Warp(new Vector3(-.55f,0,3.43f));
h.GetComponent<NavMeshAgent>().ResetPath();h.transform.rotation=Quaternion.Euler(0,270,0);
house.GetType().GetMethod("Hear",f).Invoke(house,new object[]{new Vector3(-3.5f,0,3.43f),4f});
bool permitted=(float)h.GetType().GetField("bedroomAccessUntil",f).GetValue(h)>Time.time;
entered=false;UnityEditor.EditorApplication.update+=watch;
await System.Threading.Tasks.Task.Delay(3500);
UnityEditor.EditorApplication.update-=watch;
foreach(var d in devices)if(d.added)InputSystem.EnableDevice(d);
var results=new object[]{new{name="Open bedroom remains safe during 25 seconds of silent patrol",passed=quietSafe,detail=frames},
 new{name="Heard room noise permits Anon to enter",passed=permitted&&entered,detail=new{permitted,entered}}};
System.IO.File.WriteAllText("output/bedroom_upgrade/bedroom_safety_checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
return results;

