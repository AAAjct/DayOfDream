using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DayOfDream.Horror;
if(!UnityEditor.EditorApplication.isPlaying)throw new Exception("Play mode required");
const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
var results=new System.Collections.Generic.List<object>();
void Check(string name,bool passed,object detail)=>results.Add(new{name,passed,detail});
var p=UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
var house=UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
var audio=p.GetComponent<ExplorerAudio>();
var inv=p.GetComponent<LeftHandInventory>();
var all=UnityEngine.Object.FindObjectsByType<HouseObject>(FindObjectsInactive.Include);
var key=all.First(x=>Get(x,"kind").ToString()=="CabinetKey");
var bottle=all.First(x=>Get(x,"kind").ToString()=="Bottle");
var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
floor.transform.position=new Vector3(50,-.25f,50);floor.transform.localScale=new Vector3(50,.5f,70);
var simulation=Physics.simulationMode;
try
{
 Set(p,"<IsWaking>k__BackingField",false);Set(house,"grace",1000f);p.SetPaused(false);
 Call(p,"Warp",new Vector3(50,.04f,30),Quaternion.identity,null);
 Set(p,"grounded",true);Set(p,"<MovementSpeed>k__BackingField",2.4f);
 int before=(int)Get(audio,"<FootstepsPlayed>k__BackingField");
 for(int i=0;i<Mathf.CeilToInt(1.5f/Mathf.Max(.0001f,Time.deltaTime));i++)Call(audio,"Tick",true);
 Check("Player hears walking footsteps",(int)Get(audio,"<FootstepsPlayed>k__BackingField")>before,Get(audio,"<FootstepsPlayed>k__BackingField"));
 Set(p,"<MovementSpeed>k__BackingField",4.5f);
 before=(int)Get(audio,"<FootstepsPlayed>k__BackingField");
 for(int i=0;i<Mathf.CeilToInt(1.5f/Mathf.Max(.0001f,Time.deltaTime));i++)Call(audio,"Tick",true);
 Check("Player hears running footsteps",(int)Get(audio,"<FootstepsPlayed>k__BackingField")>before,Get(audio,"<FootstepsPlayed>k__BackingField"));
 Set(p,"<MovementSpeed>k__BackingField",2.4f);Set(house,"nextNoise",0f);Call(house,"Update");
 Check("Walking generates no enemy hearing event",(float)Get(house,"nextNoise")==0,Get(house,"nextNoise"));
 var execution=(AudioSource)Get(audio,"execution");
 Check("Supplied execution clip loaded",execution.clip!=null&&execution.clip.name=="ExecutionLaugh",execution.clip?.length);
 Physics.simulationMode=SimulationMode.Script;
 Call(house,"Use",key);Call(inv,"Tick",true);
 var grip=(Transform)Get(inv,"grip");
 Check("Selected item uses left-hand anchor",key.transform.parent==grip&&grip.localPosition.x<0,grip.localPosition.ToString());
 Call(house,"ReleaseSlot",0);
 Call(key,"Launch",new Vector3(50,1.5f,30),new Vector3(0,0,1.3f),house);
 Set(key,"nextImpact",0f);Physics.SyncTransforms();
 for(int i=0;i<125;i++)Physics.Simulate(.02f);
 float shortZ=key.transform.position.z;
 Check("Dropped key collides with floor",key.transform.position.y>-.1f&&key.transform.position.y<.4f,key.transform.position.ToString());
 Call(house,"Use",key);
 Check("Dropped key can be collected again",(HouseObject)Call(house,"GetSlot",0)==key,"slot 1 restored");
 Call(house,"ReleaseSlot",0);Call(key,"Launch",new Vector3(50,1.5f,30),new Vector3(0,4,13),house);
 Set(key,"nextImpact",0f);Physics.SyncTransforms();
 for(int i=0;i<125;i++)Physics.Simulate(.02f);
 Check("Charged throw lands farther",key.transform.position.z>shortZ+4,new{shortZ,longZ=key.transform.position.z,y=key.transform.position.y});
 Call(bottle,"Launch",new Vector3(50,1.5f,30),new Vector3(0,1,3),house);
 Set(bottle,"nextImpact",0f);Physics.SyncTransforms();
 for(int i=0;i<100;i++)Physics.Simulate(.02f);
 Check("Bottle remains visible on the floor",bottle.gameObject.activeInHierarchy&&bottle.GetComponentsInChildren<Renderer>().Any(r=>r.enabled)
     &&bottle.transform.position.y>0&&bottle.transform.position.y<.3f,bottle.transform.position.ToString());
 Call(house,"Use",bottle);
 Check("Dropped bottle can be collected again",(HouseObject)Call(house,"GetSlot",3)==bottle,"slot 4 restored");
 Call(audio,"Execute");
 Check("Execution sound plays",execution.isPlaying,"audio source playing");
}
finally
{
 Physics.simulationMode=simulation;UnityEngine.Object.Destroy(floor);
 System.IO.Directory.CreateDirectory("output/inventory_audio");
 System.IO.File.WriteAllText("output/inventory_audio/atomic_checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
}
return results;
