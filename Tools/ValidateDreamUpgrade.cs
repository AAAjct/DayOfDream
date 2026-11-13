using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using DayOfDream.Horror;

if (!UnityEditor.EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "MainMenu") throw new Exception("Run from MainMenu in Play Mode.");
const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
object Field(object target,string name) => target.GetType().GetField(name,flags).GetValue(target);
object Call(object target,string name,params object[] args) => target.GetType().GetMethod(name,flags).Invoke(target,args);
void Set(object target,string name,object value) => target.GetType().GetField(name,flags).SetValue(target,value);
var results = new List<object>();
void Check(string name,bool passed,object detail=null) => results.Add(new {name,passed,detail});
var output = Path.GetFullPath("output/dream_polish"); Directory.CreateDirectory(output);
var savePath = Path.Combine(Application.persistentDataPath,"dream-checkpoint.json");
string oldSave = File.Exists(savePath) ? File.ReadAllText(savePath) : null;
float oldVolume = PlayerPrefs.GetFloat("Dream.Volume",.8f), oldSensitivity = PlayerPrefs.GetFloat("Dream.Sensitivity",1);
var realDevices = InputSystem.devices.Where(d=>d.enabled && (d is Keyboard || d is Mouse || d is Gamepad)).ToArray();
foreach(var device in realDevices) InputSystem.DisableDevice(device);
var keyboard = InputSystem.AddDevice<Keyboard>();
var background = InputSystem.settings.backgroundBehavior;
InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
var errors = new List<string>();
Application.LogCallback logger = (message,stack,type) => { if(type == LogType.Error || type == LogType.Exception) errors.Add(message); };
Application.logMessageReceived += logger;
var menu = UnityEngine.Object.FindAnyObjectByType<DreamMainMenu>();
var lamp = GameObject.Find("Flickering doorway light").GetComponent<Light>();
float minLamp = 100, maxLamp = 0;
int firstFrame = Time.frameCount, stage = 0;
double begin = UnityEditor.EditorApplication.timeSinceStartup, since = begin;
FirstPersonExplorer player = null;
AnonHouse house = null;
HandheldCamcorder dv = null;
HouseObject key = null, bottle = null;
Vector3 resumePosition = Vector3.zero;
void Next() { stage++; since = UnityEditor.EditorApplication.timeSinceStartup; }
UnityEditor.EditorApplication.CallbackFunction tick = null;
void Cleanup()
{
    UnityEditor.EditorApplication.update -= tick;
    Application.logMessageReceived -= logger;
    if(keyboard.added) InputSystem.RemoveDevice(keyboard);
    foreach(var device in realDevices) if(device.added) InputSystem.EnableDevice(device);
    InputSystem.settings.backgroundBehavior = background;
    PlayerPrefs.SetFloat("Dream.Volume",oldVolume); PlayerPrefs.SetFloat("Dream.Sensitivity",oldSensitivity); PlayerPrefs.DeleteKey("Dream.Continue");
    AudioListener.volume = oldVolume;
    if(oldSave != null) File.WriteAllText(savePath,oldSave); else if(File.Exists(savePath)) File.Delete(savePath);
    PlayerPrefs.Save();
}
tick = () =>
{
    try
    {
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (player != null && SceneManager.GetActiveScene().name == "AnonHouse") player.SetPaused(false);
        if(now-begin > 110) throw new Exception("Validation timed out at stage "+stage);
        switch(stage)
        {
            case 0:
                minLamp = Mathf.Min(minLamp,lamp.intensity); maxLamp = Mathf.Max(maxLamp,lamp.intensity);
                if(now-since < 3) return;
                Check("Player loop advances",Time.frameCount > firstFrame+5,Time.frameCount-firstFrame);
                Check("Door lamp visibly flickers",maxLamp-minLamp>2,new {minLamp,maxLamp});
                ((Button)Field(menu,"settingsButton")).onClick.Invoke();
                Check("Settings opens",((GameObject)Field(menu,"settingsPanel")).activeSelf);
                ((Slider)Field(menu,"volumeSlider")).value = .35f;
                ((Slider)Field(menu,"sensitivitySlider")).value = 1.4f;
                Check("Settings apply",Mathf.Abs(AudioListener.volume-.35f)<.01f && Mathf.Abs(PlayerPrefs.GetFloat("Dream.Sensitivity")-1.4f)<.01f);
                ((Button)Field(menu,"backButton")).onClick.Invoke();
                Check("Settings closes",!((GameObject)Field(menu,"settingsPanel")).activeSelf);
                ((Button)Field(menu,"startButton")).onClick.Invoke(); Next(); break;
            case 1:
                if(SceneManager.GetActiveScene().name != "AnonHouse") return;
                player = UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
                house = UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
                dv = player.GetComponentInChildren<HandheldCamcorder>();
                UnityEngine.Object.FindAnyObjectByType<AnonHunter>(FindObjectsInactive.Include)?.gameObject.SetActive(false);
                Set(house,"grace",1000f); player.SetPaused(false);
                if((bool)Field(player,"<IsWaking>k__BackingField")) return;
                Check("Start loads house",true);
                var lights = UnityEngine.Object.FindObjectsByType<Light>().Where(l=> l.transform.position.x>-7 && l.transform.position.x< -1.43f && l.transform.position.z>0 && l.transform.position.z<7 && l.transform.position.y>2 && l.transform.position.y<3.2f).ToArray();
                Check("Bedroom practical is brighter",lights.Count(l=>l.enabled)==1 && lights.Single(l=>l.enabled).intensity>=1f,lights.Select(l=>new{l.name,l.enabled,l.intensity}).ToArray());
                Check("Quilt exists",GameObject.Find("Draped quilt") != null);
                var hud = player.GetComponentInChildren<HorrorHud>();
                var slots = (Image[])Field(hud,"inventorySlots");
                var icons = (Image[])Field(hud,"inventoryIcons");
                Check("Empty slots have no text",slots.All(s=>s.GetComponentsInChildren<TMPro.TMP_Text>(true).Length==0) && icons.All(i=>!i.enabled));
                Check("All item image assets load",icons.All(i=>i.sprite!=null));
                var pickup = (CamcorderPickup)Field(dv,"pickup"); Call(pickup,"TryPickUp",dv);
                Check("Battery pickups removed",UnityEngine.Object.FindObjectsByType<CamcorderBattery>().Length==0);
                dv.ToggleRaised(); dv.ToggleNightVision();
                for (int i=0;i<240;i++) dv.Tick(1,0,0,false);
                Check("DV infrared remains active after four minutes",dv.InfraredActive);
                dv.ToggleNightVision(); dv.Lower();
                key = UnityEngine.Object.FindObjectsByType<HouseObject>(FindObjectsInactive.Include).First(i=>(HouseObject.Kind)Field(i,"kind")==HouseObject.Kind.CabinetKey);
                bottle = UnityEngine.Object.FindObjectsByType<HouseObject>().First(i=>(HouseObject.Kind)Field(i,"kind")==HouseObject.Kind.Bottle);
                Call(house,"Use",key); Call(house,"Use",bottle);
                Call(player,"Warp",new Vector3(-4.58f,.025f,5.1f),Quaternion.Euler(0,235,0),null);
                Next(); break;
            case 2:
                if(now-since<1) return;
                var display = player.GetComponentInChildren<HorrorHud>();
                Check("Pickup uses unlit UI image",((Image[])Field(display,"inventoryIcons"))[0].enabled &&
                    ((Image[])Field(display,"inventoryIcons"))[0].material.shader.name == "UI/Default");
                Check("Battery readouts removed",!display.GetComponentsInChildren<Transform>(true).Any(t=>
                    t.name == "Battery status" || t.name == "Battery outline" || t.name == "Spare batteries"));
                ScreenCapture.CaptureScreenshot(output+"/bedroom-inventory.png");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit4)); Next(); break;
            case 3:
                if(now-since<.15) return;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q)); Next(); break;
            case 4:
                if(now-since<.5) return;
                Check("Q shows no trajectory",!UnityEngine.Object.FindObjectsByType<LineRenderer>().Any(r=>r.name=="Throw trajectory" && r.enabled));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); Next(); break;
            case 5:
                if(now-since<.3) return;
                Check("Q drops selected objective",Call(house,"GetSlot",3)==null);
                Check("Q preserves DV",dv.IsCarried);
                Call(dv,"DropAtDeath",player.transform.position);
                Check("Permanent equipment survives death",dv.IsCarried);
                resumePosition = player.transform.position;
                Call(house,"SaveProgress");
                Check("Checkpoint saved",DreamProgress.HasSave);
                DreamProgress.RequestContinue(); SceneManager.LoadScene("AnonHouse"); Next(); break;
            case 6:
                if(now-since<1) return;
                player = UnityEngine.Object.FindAnyObjectByType<FirstPersonExplorer>();
                house = UnityEngine.Object.FindAnyObjectByType<AnonHouse>();
                dv = player.GetComponentInChildren<HandheldCamcorder>();
                UnityEngine.Object.FindAnyObjectByType<AnonHunter>(FindObjectsInactive.Include)?.gameObject.SetActive(false); Set(house,"grace",1000f);
                player.SetPaused(false);
                Check("Continue restores position",Vector3.Distance(player.transform.position,resumePosition)<.2f,new {saved=resumePosition.ToString(),restored=player.transform.position.ToString()});
                Check("Continue restores key and equipment",Call(house,"GetSlot",0)!=null && dv.IsCarried);
                Check("Continue skips opening wake",!(bool)Field(player,"<IsWaking>k__BackingField"));
                Check("No runtime errors",errors.Count==0,errors.ToArray());
                File.WriteAllText(output+"/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
                Cleanup(); break;
        }
    }
    catch(Exception exception)
    {
        Check("Validation exception",false,exception.ToString());
        File.WriteAllText(output+"/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
        Cleanup();
    }
};
UnityEditor.EditorApplication.update += tick;
return "Validation scheduled";
