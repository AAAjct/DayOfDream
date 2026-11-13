if(!UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Run route validation in Play mode.");
var player=UnityEngine.Object.FindAnyObjectByType<DayOfDream.Horror.FirstPersonExplorer>();
var motor=player.GetComponent<UnityEngine.CharacterController>();
var results=new System.Collections.Generic.List<object>();
var blocked=new System.Collections.Generic.List<object>();
int passCount=0;
UnityEngine.Physics.SyncTransforms();
bool Clear(UnityEngine.Vector3 a,UnityEngine.Vector3 b,out UnityEngine.RaycastHit hit)
{
    var low=a+UnityEngine.Vector3.up*.35f;var high=a+UnityEngine.Vector3.up*1.50f;
    return !UnityEngine.Physics.CapsuleCast(low,high,.27f,(b-a).normalized,out hit,(b-a).magnitude,~(1<<2),UnityEngine.QueryTriggerInteraction.Ignore);
}
for(int floor=0;floor<3;floor++)
{
    float y=.55f+4.2f*floor;
    for(int block=0;block<3;block++)
    {
        int bays=block==0?16:8;
        for(int bay=0;bay<bays;bay++)
        {
            float c=-bays*2.1f+(bay+.5f)*4.2f+1.63f;
            UnityEngine.Vector3 start,end;
            if(block==0){start=new UnityEngine.Vector3(-c,y,-27.3f);end=new UnityEngine.Vector3(-c,y,-24.75f);}
            else if(block==1){start=new UnityEngine.Vector3(34.3f,y,.6f-c);end=new UnityEngine.Vector3(31.8f,y,.6f-c);}
            else{start=new UnityEngine.Vector3(-34.3f,y,.6f+c);end=new UnityEngine.Vector3(-31.8f,y,.6f+c);}
            bool clear=Clear(start,end,out var hit);
            if(clear)passCount++;else blocked.Add(new{floor=floor+1,block,bay,hit=hit.collider.name,distance=hit.distance,start=start.ToString()});
        }
    }
}
results.Add(new{name="Classroom entrances",passed=passCount==96,checkedEntrances=96,clear=passCount,blocked});
for(int floor=0;floor<4;floor++)
{
    float y=.55f+floor*4.2f;
    bool clear=Clear(new UnityEngine.Vector3(-33,y,-27.3f),new UnityEngine.Vector3(33,y,-27.3f),out var hit);
    results.Add(new{name="Continuous main corridor / roof",floor=floor+1,passed=clear,hit=clear?"":hit.collider.name});
    foreach(int side in new[]{-1,1})
    {
        var from=new UnityEngine.Vector3(side*34.3f,y,-16.6f);var to=new UnityEngine.Vector3(side*34.3f,y,16.5f);
        bool wing=Clear(from,to,out var obstacle);
        results.Add(new{name="Continuous side-wing corridor / roof",floor=floor+1,side,passed=wing,hit=wing?"":obstacle.collider.name});
    }
}
player.enabled=false;
var routes=new System.Collections.Generic.List<UnityEngine.Vector3[]>();
foreach(int side in new[]{-1,1})
{
    var points=new System.Collections.Generic.List<UnityEngine.Vector3>();
    points.Add(new UnityEngine.Vector3(side*37,.55f,-16.8f));
    for(int floor=1;floor<4;floor++)
    {
        float top=.55f+floor*4.2f;
        points.Add(new UnityEngine.Vector3(side*37,top,-24.6f));
        points.Add(new UnityEngine.Vector3(side*35.1f,top,-27.3f));
        points.Add(new UnityEngine.Vector3(side*32.0f,top,-27.3f));
        if(floor<3)
        {
            points.Add(new UnityEngine.Vector3(side*35.5f,top,-27.3f));
            points.Add(new UnityEngine.Vector3(side*39.5f,top,-26));
            points.Add(new UnityEngine.Vector3(side*39.5f,top,-16.6f));
            points.Add(new UnityEngine.Vector3(side*37,top,-16.6f));
        }
    }
    routes.Add(points.ToArray());
}
int route=0,waypoint=0,lastFrame=-1;float vertical=0,lastChange=UnityEngine.Time.realtimeSinceStartup;
void Place(int which)
{
    motor.enabled=false;player.transform.position=new UnityEngine.Vector3(which==0?-37:37,.22f,-14.8f);motor.enabled=true;
    vertical=0;waypoint=0;lastChange=UnityEngine.Time.realtimeSinceStartup;
}
Place(0);
UnityEditor.EditorApplication.CallbackFunction step=null;
void Finish()
{
    UnityEditor.EditorApplication.update-=step;
    if(player!=null){player.enabled=true;player.Respawn();player.SetPaused(true);}
    System.IO.File.WriteAllText("Docs/Validation/upstairs_routes.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
    UnityEngine.Debug.Log("School route validation complete.");
}
step=()=>
{
    if(!UnityEditor.EditorApplication.isPlaying||player==null){Finish();return;}
    if(lastFrame==UnityEngine.Time.frameCount)return;lastFrame=UnityEngine.Time.frameCount;
    float dt=UnityEngine.Mathf.Min(.035f,UnityEngine.Time.deltaTime);
    var target=routes[route][waypoint];var flat=target-player.transform.position;flat.y=0;
    if(motor.isGrounded&&vertical<0)vertical=-2;vertical-=20*dt;
    var direction=flat.magnitude<.12f?UnityEngine.Vector3.zero:flat.normalized;
    motor.Move((direction*4.5f+UnityEngine.Vector3.up*vertical)*dt);
    if(flat.magnitude<.18f)
    {
        bool height=UnityEngine.Mathf.Abs(player.transform.position.y-target.y)<.25f;
        results.Add(new{name="Walk route waypoint",side=route==0?"east":"west",waypoint,passed=height,expectedY=target.y,position=player.transform.position.ToString()});
        waypoint++;lastChange=UnityEngine.Time.realtimeSinceStartup;
        if(waypoint>=routes[route].Length){route++;if(route>=routes.Count){Finish();return;}Place(route);}
    }
    else if(UnityEngine.Time.realtimeSinceStartup-lastChange>10)
    {
        results.Add(new{name="Walk route blocked",side=route==0?"east":"west",waypoint,passed=false,target=target.ToString(),position=player.transform.position.ToString()});
        route++;if(route>=routes.Count){Finish();return;}Place(route);
    }
};
UnityEditor.EditorApplication.update+=step;
return new{classroomEntrances=passCount,blocked=blocked.Count,walking="Testing both external stairs from the ground to floors 2, 3 and the roof."};
