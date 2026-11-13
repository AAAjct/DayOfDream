using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using DayOfDream.Horror;
const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
object Field(object o,string n)=>o.GetType().GetField(n,flags).GetValue(o);
object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
var house=UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
var inventory=player.GetComponent<LeftHandInventory>();
var dv=player.GetComponentInChildren<HandheldCamcorder>();
var map=((InputAction[])Field(inventory,"slots"))[0].actionMap;
var previousDevices=map.devices;
var background=InputSystem.settings.backgroundBehavior;
var keyboard=InputSystem.AddDevice<Keyboard>();
bool wasEnabled=player.enabled;
try
{
 player.enabled=false;
 InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
 InputSystem.EnableDevice(keyboard);
 map.devices=new InputDevice[]{keyboard}; map.Enable();
 var bottle=UnityEngine.Object.FindObjectsByType<HouseObject>(FindObjectsInactive.Include).First(x=>(HouseObject.Kind)Field(x,"kind")==HouseObject.Kind.Bottle);
 house.GetType().GetField("bottle",flags).SetValue(house,bottle);
 Call(bottle,"Stow");
 // The integration boundary is the inventory input tick; no window focus is needed here.
 void Keys(params Key[] keys)
 {
     InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
     typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});
     Call(inventory,"Tick",true);
 }
 Keys();Keys(Key.Digit4);Keys();
 int selected=(int)inventory.GetType().GetProperty("SelectedSlot",flags).GetValue(inventory);
 var pickup=(CamcorderPickup)Field(dv,"pickup");
 if(!dv.IsCarried)Call(pickup,"TryPickUp",dv);
 Keys(Key.Q);
 bool charging=(bool)Field(inventory,"charging");
 bool noTrajectory=!UnityEngine.Object.FindObjectsByType<LineRenderer>().Any(r=>r.name=="Throw trajectory"&&r.enabled);
 Keys();
 bool dropped=Call(house,"GetSlot",3)==null;
 var result=new{selected,charging,noTrajectory,dropped,equipmentRetained=dv.IsCarried,passed=selected==3&&charging&&noTrajectory&&dropped&&dv.IsCarried};
 System.IO.File.WriteAllText("output/dream_upgrade/drop-atomic.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
 return result;
}
finally
{
 map.devices=previousDevices;
 InputSystem.settings.backgroundBehavior=background;
 InputSystem.RemoveDevice(keyboard);
 player.enabled=wasEnabled;
}
