if (!UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Run this validation in Play mode.");
var player = UnityEngine.Object.FindAnyObjectByType<DayOfDream.Horror.FirstPersonExplorer>();
var motor = player.GetComponent<UnityEngine.CharacterController>();
var view = player.GetComponentInChildren<UnityEngine.Camera>();
var lamp = player.GetComponentInChildren<DayOfDream.Horror.HandheldFlashlight>();
var doors = UnityEngine.Object.FindObjectsByType<DayOfDream.Horror.HingedDoor>();
var realDevices=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(UnityEngine.InputSystem.InputSystem.devices,d=>d.enabled&&(d is UnityEngine.InputSystem.Keyboard||d is UnityEngine.InputSystem.Mouse||d is UnityEngine.InputSystem.Gamepad)));
foreach(var device in realDevices)UnityEngine.InputSystem.InputSystem.DisableDevice(device);
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
var background = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
var results = new System.Collections.Generic.List<object>();
var errors = new System.Collections.Generic.List<string>();
int stage=0,previousFrame=-1;float since=UnityEngine.Time.realtimeSinceStartup;
UnityEngine.Vector3 origin=player.transform.position;float minY=100,maxY=-100,walkDistance=0,groundY=0,peakY=0;
if(doors.Length>0&&doors[0].IsOpen)doors[0].Toggle();
void Keys(params UnityEngine.InputSystem.Key[] keys) => UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
void Check(string name,bool passed,object detail) { results.Add(new {name,passed,detail}); }
void Teleport(UnityEngine.Vector3 position,float yaw)
{
    Keys();motor.enabled=false;player.transform.SetPositionAndRotation(position,UnityEngine.Quaternion.Euler(0,yaw,0));motor.enabled=true;
    view.transform.localPosition=new UnityEngine.Vector3(0,1.6f,.055f);view.transform.localRotation=UnityEngine.Quaternion.identity;
    UnityEngine.Physics.SyncTransforms();player.SetPaused(false);
}
void Next() {stage++;since=UnityEngine.Time.realtimeSinceStartup;}
UnityEngine.Application.LogCallback logs=(message,stack,type)=>{if(type==UnityEngine.LogType.Error||type==UnityEngine.LogType.Exception)errors.Add(message);};
UnityEngine.Application.logMessageReceived+=logs;
Teleport(new UnityEngine.Vector3(0,.2f,24),180);
UnityEditor.EditorApplication.CallbackFunction step=null;
step=()=>
{
    if(!UnityEditor.EditorApplication.isPlaying||player==null)
    {
        UnityEditor.EditorApplication.update-=step;UnityEngine.Application.logMessageReceived-=logs;
        if(keyboard.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
        if(mouse.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
        foreach(var device in realDevices)if(device.added)UnityEngine.InputSystem.InputSystem.EnableDevice(device);
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;return;
    }
    if(previousFrame==UnityEngine.Time.frameCount)return;previousFrame=UnityEngine.Time.frameCount;
    float elapsed=UnityEngine.Time.realtimeSinceStartup-since;
    try
    {
        switch(stage)
        {
            case 0:
                if(elapsed<.5f)return;
                Check("Spawn grounded",player.IsGrounded,player.transform.position.ToString());
                origin=player.transform.position;Keys(UnityEngine.InputSystem.Key.W);Next();break;
            case 1:
                minY=UnityEngine.Mathf.Min(minY,view.transform.position.y);maxY=UnityEngine.Mathf.Max(maxY,view.transform.position.y);
                if(elapsed<1.1f)return;
                walkDistance=UnityEngine.Vector3.Distance(origin,player.transform.position);
                Check("WASD walking",walkDistance>1.8f,walkDistance);
                Check("Paving camera stability",maxY-minY<.015f,maxY-minY);
                origin=player.transform.position;minY=100;maxY=-100;
                Keys(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.LeftShift);Next();break;
            case 2:
                if(elapsed>.35f) { minY=UnityEngine.Mathf.Min(minY,view.transform.localPosition.y);maxY=UnityEngine.Mathf.Max(maxY,view.transform.localPosition.y); }
                if(elapsed<1.1f)return;
                float runDistance=UnityEngine.Vector3.Distance(origin,player.transform.position);
                Check("Sprint faster than walk",runDistance>walkDistance*1.45f,runDistance);
                Check("Sprint produces controlled head bob",maxY-minY>.025f&&maxY-minY<.08f,maxY-minY);
                Keys(UnityEngine.InputSystem.Key.LeftCtrl);Next();break;
            case 3:
                if(elapsed<.3f)return;
                Check("Ctrl crouches",player.IsCrouched&&motor.height<1.2f,motor.height);
                Keys();Next();break;
            case 4:
                if(elapsed<.3f)return;
                Check("Stand after release",!player.IsCrouched&&motor.height>1.7f,motor.height);
                if(lamp!=null&&!lamp.IsOn)lamp.Toggle();Keys(UnityEngine.InputSystem.Key.L);Next();break;
            case 5:
                if(elapsed<.2f)return;
                if(lamp!=null)Check("L switches flashlight off",!lamp.IsOn,lamp.IsOn);Keys();Next();break;
            case 6:
                if(elapsed<.15f)return;Keys(UnityEngine.InputSystem.Key.L);Next();break;
            case 7:
                if(elapsed<.2f)return;
                if(lamp!=null)Check("L switches flashlight on",lamp.IsOn,lamp.IsOn);
                Teleport(new UnityEngine.Vector3(.65f,.68f,-13),180);Next();break;
            case 8:
                if(elapsed<.3f)return;
                bool ray=UnityEngine.Physics.Raycast(view.transform.position,view.transform.forward,out var hit,2.5f,~(1<<2));
                Check("Door interaction ray hits a door",ray&&hit.collider.GetComponentInParent<DayOfDream.Horror.HingedDoor>()!=null,ray?hit.collider.name:"no hit");
                Keys(UnityEngine.InputSystem.Key.E);Next();break;
            case 9:
                if(elapsed<1.1f)return;
                Check("E opens both door leaves",System.Linq.Enumerable.All(doors,d=>d.IsOpen),System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(doors,d=>d.transform.eulerAngles.y)));
                Keys(UnityEngine.InputSystem.Key.W);origin=player.transform.position;Next();break;
            case 10:
                if(elapsed<2.2f)return;
                Check("Walk through open entrance and into building",player.transform.position.z < -17.8f,player.transform.position.ToString());
                Teleport(new UnityEngine.Vector3(8,.6f,-15),180);Keys(UnityEngine.InputSystem.Key.W);Next();break;
            case 11:
                if(elapsed<1.3f)return;
                Check("Facade collision blocks player",player.transform.position.z > -17.1f&&player.transform.position.z < -16,player.transform.position.ToString());
                Teleport(new UnityEngine.Vector3(0,.2f,24),180);Keys(UnityEngine.InputSystem.Key.Escape);Next();break;
            case 12:
                if(elapsed<.2f)return;
                Check("Esc pauses movement",player.IsPaused,player.IsPaused);Keys();Next();break;
            case 13:
                if(elapsed<.15f)return;Keys(UnityEngine.InputSystem.Key.Escape);Next();break;
            case 14:
                if(elapsed<.2f)return;
                Check("Esc resumes movement",!player.IsPaused,player.IsPaused);
                Teleport(new UnityEngine.Vector3(0,.2f,24),180);Next();break;
            case 15:
                if(elapsed<.3f)return;
                groundY=player.transform.position.y;peakY=groundY;Keys(UnityEngine.InputSystem.Key.Space);Next();break;
            case 16:
                peakY=UnityEngine.Mathf.Max(peakY,player.transform.position.y);
                if(elapsed<.35f)return;
                Check("Space does not jump",player.IsGrounded&&peakY-groundY<.03f,peakY-groundY);
                origin=player.transform.position;
                Keys(UnityEngine.InputSystem.Key.LeftAlt,UnityEngine.InputSystem.Key.W);Next();break;
            case 17:
                if(elapsed<.55f)return;
                Check("Alt looks back",UnityEngine.Vector3.Dot(player.transform.forward,view.transform.forward)<-.99f,view.transform.localEulerAngles.y);
                Check("Looking back preserves forward movement",UnityEngine.Vector3.Dot(player.transform.position-origin,player.transform.forward)>.8f,player.transform.position.ToString());
                Keys();Next();break;
            case 18:
                if(elapsed<.55f)return;
                Check("Releasing Alt restores forward view",UnityEngine.Vector3.Dot(player.transform.forward,view.transform.forward)>.99f,view.transform.localEulerAngles.y);
                Keys();player.Respawn();player.SetPaused(false);Next();break;
            default:
                Check("No runtime errors",errors.Count==0,errors.ToArray());
                UnityEditor.EditorApplication.update-=step;UnityEngine.Application.logMessageReceived-=logs;
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                foreach(var device in realDevices)if(device.added)UnityEngine.InputSystem.InputSystem.EnableDevice(device);
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;
                System.IO.Directory.CreateDirectory("Docs/Validation");
                System.IO.File.WriteAllText("Docs/Validation/exploration_checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
                UnityEngine.Debug.Log("Exploration validation complete: Docs/Validation/exploration_checks.json");break;
        }
    }
    catch(System.Exception ex)
    {
        UnityEditor.EditorApplication.update-=step;UnityEngine.Application.logMessageReceived-=logs;
        UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
        foreach(var device in realDevices)if(device.added)UnityEngine.InputSystem.InputSystem.EnableDevice(device);
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;
        System.IO.File.WriteAllText("Docs/Validation/exploration_checks_error.txt",ex.ToString());
    }
};
UnityEditor.EditorApplication.update+=step;
return "Started exploration runtime checks; results arrive after roughly 14 seconds.";
