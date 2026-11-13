using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace DayOfDream.Horror
{
    public sealed class AnonHouse : MonoBehaviour
    {
        [SerializeField] private FirstPersonExplorer player;
        [SerializeField] private AnonHunter hunter;
        [SerializeField] private HorrorHud hud;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private Light exitLamp;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip crash, creak;
        [SerializeField] private Bounds[] noisyBoards;
        [SerializeField] private Transform wakePillow, wakeExit;

        private HouseObject hiding;
        private WardrobeDoors wardrobeDoors;
        private float wardrobeTime;
        private bool leavingWardrobe;
        private int day = 1;
        private HouseObject cabinetKey, exitKey, fuse, bottle;
        private bool powered, ended;
        private float grace = 8, blackout, nextNoise, feedbackUntil;
        public bool BlocksMovement => hiding != null || IsExecuting || player.IsWaking || blackout > 0 || ended;
        internal bool Hidden => hiding != null;
        internal bool IsUnderBed => hiding != null && hiding.Type == HouseObject.Kind.HideUnderBed;
        internal bool HasCabinetKey => cabinetKey != null;
        internal bool HasExitKey => exitKey != null;
        internal bool HasFuse => fuse != null;
        internal bool HasBottle => bottle != null;
        internal HouseObject GetSlot(int slot) => slot switch { 0 => cabinetKey, 1 => exitKey, 2 => fuse, 3 => bottle, _ => null };
        internal HouseObject ReleaseSlot(int slot)
        {
            HouseObject item = GetSlot(slot);
            switch (slot) { case 0: cabinetKey = null; break; case 1: exitKey = null; break; case 2: fuse = null; break; case 3: bottle = null; break; }
            return item;
        }
        private void Collect(HouseObject item)
        {
            item.Stow();
            player.GetComponent<ExplorerAudio>()?.PickUp();
        }
        internal bool IsExecuting { get; private set; }
        internal bool Running => !IsExecuting && !player.IsWaking && !ended && blackout <= 0 && !player.IsPaused;
        internal bool CanHunt => Running && grace <= 0;

        private void Start()
        {
            var checkpoint = DreamProgress.ConsumeContinue();
            if (checkpoint != null) { RestoreProgress(checkpoint); return; }
            if (wakePillow != null && wakeExit != null) player.BeginWake(wakePillow, wakeExit);
        }

        private float nextSave = 10;
        internal void SaveProgress()
        {
            if (player == null || !Running || Hidden || player.IsCrouched) return;
            var camera = player.GetComponentInChildren<HandheldCamcorder>();
            var objects = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseObject>(true)).ToArray();
            var cabinet = objects.FirstOrDefault(item => item.Type == HouseObject.Kind.Cabinet);
            DreamProgress.Write(new DreamProgress.Snapshot
            {
                day = day, powered = powered, cabinetOpen = cabinet != null && !cabinet.gameObject.activeSelf,
                position = player.transform.position, rotation = player.transform.rotation,
                dv = camera != null && camera.IsCarried,
                slots = Enumerable.Range(0, 4).Select(i => GetSlot(i)?.SaveId).ToArray(),
                items = objects.Select(item => new DreamProgress.Item { id = item.SaveId, position = item.transform.position,
                    rotation = item.transform.rotation, active = item.gameObject.activeSelf }).ToArray()
            });
        }

        private void RestoreProgress(DreamProgress.Snapshot save)
        {
            var objects = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseObject>(true)).ToArray();
            var restoredSlots = new HouseObject[4];
            foreach (var item in objects)
            {
                string id = item.SaveId;
                var state = System.Array.Find(save.items, value => value.id == id);
                int slot = System.Array.IndexOf(save.slots, id);
                if (slot >= 0) { restoredSlots[slot] = item; item.Stow(); continue; }
                if (state != null)
                {
                    item.transform.SetPositionAndRotation(state.position, state.rotation);
                    item.gameObject.SetActive(state.active);
                }
                else if (item.Type is HouseObject.Kind.Bottle) item.gameObject.SetActive(false);
            }
            cabinetKey = restoredSlots[0]; exitKey = restoredSlots[1]; fuse = restoredSlots[2]; bottle = restoredSlots[3];
            day = Mathf.Clamp(save.day, 1, 5); powered = save.powered;
            if (powered && exitLamp != null) exitLamp.color = new Color(.33f, 1, .55f);
            var cabinet = objects.FirstOrDefault(item => item.Type == HouseObject.Kind.Cabinet);
            if (cabinet != null && save.cabinetOpen)
            {
                cabinet.gameObject.SetActive(false);
                if (cabinet.Reveal != null) cabinet.Reveal.SetActive(true);
            }
            player.Warp(save.position, save.rotation);
            player.GetComponentInChildren<HandheldCamcorder>()?.RestoreEquipment(save.dv);
            grace = 8;
        }

        private void OnApplicationQuit() => SaveProgress();

        private void Update()
        {
            if (Time.time >= nextSave) { nextSave = Time.time + 20; SaveProgress(); }
            if (ended || IsExecuting || player.IsWaking || player.IsPaused) return;
            if (blackout > 0)
            {
                blackout -= Time.deltaTime;
                if (blackout <= 0)
                {
                    hunter.ResetPatrol();
                    if (wakePillow != null && wakeExit != null) player.BeginWake(wakePillow, wakeExit);
                    else player.Respawn();
                    grace = 8;
                }
                return;
            }
            grace -= Time.deltaTime;
            if (feedbackUntil > 0 && Time.time >= feedbackUntil)
            {
                feedback.text = "";
                feedbackUntil = 0;
            }
            // Walking and crouch movement are silent, including the bedroom floorboards.
            if (Hidden || !player.IsGrounded || player.IsCrouched || player.MovementSpeed <= 3.3f || Time.time < nextNoise) return;
            nextNoise = Time.time + .50f;
            Hear(player.transform.position, 4);

        }

        internal void Hear(Vector3 point, float radius)
        {
            if (CanHunt) hunter.Hear(point, radius);
        }

        internal void BeginExecution()
        {
            IsExecuting = true;
            feedback.text = "";
            feedbackUntil = 0;
            hud.SetPrompt("");
            hud.DismissNote();
            player.PrepareForExecution();
        }

        internal void FinishExecution()
        {
            if (!IsExecuting) return;
            player.StopExecutionAudio();
            Drop(ref cabinetKey, new Vector3(-.22f, 0, .06f));
            Drop(ref exitKey, new Vector3(.22f, 0, .06f));
            Drop(ref fuse, new Vector3(0, 0, -.23f));
            Drop(ref bottle, new Vector3(0, 0, .25f));
            IsExecuting = false;
            if (day == 5)
            {
                End("第五夜结束", "Anon 找到了你。");
                return;
            }
            day++;
            blackout = 1.5f;
            hud.ShowNote($"第 {day} 天", "", 1.4f);
        }

        private void Drop(ref HouseObject item, Vector3 offset)
        {
            if (item == null) return;
            Vector3 position = player.transform.position + offset;
            if (Physics.Raycast(position + Vector3.up * .45f, Vector3.down, out var hit,
                1.45f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                position = hit.point;
            item.PlaceOnSurface(position);
            item = null;
        }

        internal void InteractWhileStopped()
        {
            if (ended)
            {
                SceneManager.LoadScene(gameObject.scene.buildIndex);
                return;
            }
            if (hiding == null) return;
            if (wardrobeDoors != null)
            {
                leavingWardrobe = true;
                wardrobeTime = 0;
                hud.SetPrompt("");
                player.BeginWardrobeMove();
                return;
            }
            var leave = hiding.Outside;
            hiding = null;
            player.Warp(leave.position, leave.rotation);
            hud.SetPrompt("");
        }

        internal bool AdvanceWardrobeMove(float deltaTime)
        {
            if (wardrobeDoors == null || wardrobeTime >= .59f) return false;
            wardrobeTime = Mathf.Min(.59f, wardrobeTime + deltaTime);
            float opening = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .22f, wardrobeTime));
            float closing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.34f, .59f, wardrobeTime));
            wardrobeDoors.SetOpening(88 * opening * (1 - closing));
            float travel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.11f, .44f, wardrobeTime));
            player.PoseWardrobeMove(hiding.Inside, hiding.Outside, !leavingWardrobe, travel);
            if (wardrobeTime < .59f) return true;

            player.FinishWardrobeMove(leavingWardrobe ? hiding.Outside : hiding.Inside, !leavingWardrobe);
            if (leavingWardrobe)
            {
                hiding = null;
                wardrobeDoors = null;
            }
            else hud.SetPrompt("", true);
            return true;
        }

        internal string Prompt(HouseObject target) => target.Type switch
        {
            HouseObject.Kind.CabinetKey => "小钥匙",
            HouseObject.Kind.ExitKey => "正门钥匙",
            HouseObject.Kind.Fuse => "保险丝",
            HouseObject.Kind.Cabinet => "铁柜",
            HouseObject.Kind.PowerBox => "电闸",
            HouseObject.Kind.Exit => "正门",
            HouseObject.Kind.Hide => "躲进衣柜",
            HouseObject.Kind.HideUnderBed => "躲到床底",
            _ => "拾取瓶子"
        };

        internal void Use(HouseObject target)
        {
            if (BlocksMovement) return;
            switch (target.Type)
            {
                case HouseObject.Kind.CabinetKey:
                    if (cabinetKey != null) break; cabinetKey = target; Collect(target); break;
                case HouseObject.Kind.ExitKey:
                    if (exitKey != null) break; exitKey = target; Collect(target); break;
                case HouseObject.Kind.Fuse:
                    if (fuse != null) break; fuse = target; Collect(target); break;
                case HouseObject.Kind.Cabinet:
                    if (!cabinetKey) { Say("缺小钥匙"); break; }
                    cabinetKey.Stow(); cabinetKey = null; target.gameObject.SetActive(false); target.Reveal.SetActive(true);
                    Hear(target.transform.position, 6); break;
                case HouseObject.Kind.PowerBox:
                    if (powered) break;
                    if (!fuse) { Say("缺保险丝"); break; }
                    fuse.Stow(); fuse = null; powered = true; exitLamp.color = new Color(.33f, 1, .55f);
                    Hear(target.transform.position, 10); break;
                case HouseObject.Kind.Exit:
                    if (powered && exitKey) End("逃出旧宅", $"你在第 {day} 夜逃了出去。");
                    else Say(!exitKey && !powered ? "缺正门钥匙 · 电闸未通电" :
                        !powered ? "电闸未通电" : "缺正门钥匙");
                    break;
                case HouseObject.Kind.Hide:
                case HouseObject.Kind.HideUnderBed:
                    if (hunter.CanSeePlayer && CanHunt)
                    {
                        Say("已被发现");
                        break;
                    }
                    hiding = target;
                    if (target.Type == HouseObject.Kind.HideUnderBed)
                    {
                        player.HideUnderBed(target.Inside);
                        hud.SetPrompt("", true);
                    }
                    else if (target.TryGetComponent(out wardrobeDoors))
                    {
                        leavingWardrobe = false;
                        wardrobeTime = 0;
                        hud.SetPrompt("");
                        player.BeginWardrobeMove();
                    }
                    else
                    {
                        player.Warp(target.Inside.position, target.Inside.rotation);
                        hud.SetPrompt("", true);
                    }
                    break;
                case HouseObject.Kind.Bottle:
                    if (bottle != null) { Say("瓶子格已满"); break; }
                    bottle = target;
                    Collect(target);
                    break;
            }
        }

        private void Say(string message)
        {
            feedback.text = message;
            feedbackUntil = Time.time + 1.8f;
        }

        private void End(string title, string message)
        {
            ended = true;
            hud.ShowNote(title, message + "\n\nE  重新开始", float.PositiveInfinity);
            hud.SetPrompt("");
        }

    }
}
