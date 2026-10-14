using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FartRaft
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene")]
        public Raft raft;
        public FartSeat[] seats = new FartSeat[4];
        public TouchFartButton[] buttons = new TouchFartButton[4];
        public Transform startPoint;
        public CameraFollow cameraFollow;
        public Vector3[] coursePath;
        public int checkpointCount = 3;
        public ParticleSystem impactEffect;

        [Header("UI")]
        public GameObject menuPanel;
        public GameObject hudPanel;
        public GameObject finishPanel;
        public Button soloButton;
        public Button twoPlayersButton;
        public Button threePlayersButton;
        public Button fourPlayersButton;
        public Button playAgainButton;
        public Button finishMenuButton;
        public Button hudMenuButton;
        public Text timerText;
        public Text messageText;
        public Text syncText;
        public Text finishText;
        public Text fallsText;
        public Image progressFill;

        [Header("Rules")]
        [Tooltip("Thrust multiplier while every player farts at the same time.")]
        public float syncBoost = 1.5f;
        [Tooltip("How far below the last checkpoint the raft can drop before it counts as a fall.")]
        public float fallDepth = 10f;

        static readonly float[] TwoSeatAngles = { 215f, 145f };
        static readonly float[] ThreeSeatAngles = { 230f, 130f, 180f };
        static readonly float[] FourSeatAngles = { 225f, 135f, 315f, 45f };
        static readonly string[] AnimalNames = { "CAPY", "HIPPO", "CROC", "GORILLA" };

        int playerCount = 2;
        bool solo;
        bool racing;
        bool syncActive;
        float raceTime;
        int falls;
        int nextCheckpoint;
        int progressIndex = -1;
        Transform respawnPoint;
        float messageHideTime;
        Coroutine countdown;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            soloButton.onClick.AddListener(() => StartGame(2, true));
            twoPlayersButton.onClick.AddListener(() => StartGame(2, false));
            threePlayersButton.onClick.AddListener(() => StartGame(3, false));
            fourPlayersButton.onClick.AddListener(() => StartGame(4, false));
            playAgainButton.onClick.AddListener(() => StartGame(playerCount, solo));
            finishMenuButton.onClick.AddListener(ShowMenu);
            hudMenuButton.onClick.AddListener(ShowMenu);
        }

        void Start()
        {
            ShowMenu();
        }

        public void ShowMenu()
        {
            StopCountdown();
            racing = false;
            SetLocked(true);
            menuPanel.SetActive(true);
            hudPanel.SetActive(false);
            finishPanel.SetActive(false);
            ConfigureSeats();
            ResetRaft(startPoint);
        }

        public void StartGame(int count, bool soloMode)
        {
            StopCountdown();
            playerCount = Mathf.Clamp(count, 2, 4);
            solo = soloMode;
            ConfigureSeats();

            menuPanel.SetActive(false);
            finishPanel.SetActive(false);
            hudPanel.SetActive(true);

            racing = false;
            raceTime = 0f;
            falls = 0;
            nextCheckpoint = 0;
            respawnPoint = startPoint;
            ResetRaft(startPoint);
            UpdateHud();
            countdown = StartCoroutine(Countdown());
        }

        IEnumerator Countdown()
        {
            SetLocked(true);
            for (int i = 3; i > 0; i--)
            {
                ShowMessage(i.ToString(), 1.1f);
                yield return new WaitForSeconds(1f);
            }
            ShowMessage("FART!", 1f);
            SetLocked(false);
            racing = true;
            countdown = null;
        }

        void StopCountdown()
        {
            if (countdown != null) StopCoroutine(countdown);
            countdown = null;
        }

        void ConfigureSeats()
        {
            float[] angles = playerCount == 2 ? TwoSeatAngles : playerCount == 3 ? ThreeSeatAngles : FourSeatAngles;

            for (int i = 0; i < seats.Length; i++)
            {
                bool active = i < playerCount;
                seats[i].gameObject.SetActive(active);
                buttons[i].gameObject.SetActive(active);
                if (!active) continue;

                seats[i].SetAngle(angles[i]);
                // Total thrust stays the same regardless of player count.
                seats[i].thrustScale = 2f / playerCount;
                seats[i].ResetGas();
                buttons[i].SetLabel(solo ? (i == 0 ? "LEFT" : "RIGHT") : AnimalNames[i]);
            }
        }

        void SetLocked(bool locked)
        {
            foreach (var seat in seats) seat.locked = locked;
            raft.SetFrozen(locked);
        }

        void ResetRaft(Transform point)
        {
            raft.Respawn(point.position, point.rotation);
            progressIndex = -1;
            if (cameraFollow != null) cameraFollow.Snap();
        }

        void FixedUpdate()
        {
            int active = 0;
            int farting = 0;
            foreach (var seat in seats)
            {
                if (!seat.gameObject.activeSelf) continue;
                active++;
                if (seat.IsFarting) farting++;
            }

            syncActive = racing && active >= 2 && farting == active;
            foreach (var seat in seats) seat.BoostMultiplier = syncActive ? syncBoost : 1f;
        }

        void Update()
        {
            if (racing)
            {
                raceTime += Time.deltaTime;
                if (raft.transform.position.y < respawnPoint.position.y - fallDepth) OnFell();
            }

            UpdateHud();
            syncText.enabled = syncActive;
            if (messageText.enabled && Time.time > messageHideTime) messageText.enabled = false;
        }

        void OnFell()
        {
            falls++;
            ShowMessage("WHOOPS! Fell off", 1.4f);
            ResetRaft(respawnPoint);
        }

        public void PlayImpact(Vector3 position)
        {
            if (impactEffect == null) return;
            impactEffect.transform.position = position + Vector3.up * 0.8f;
            impactEffect.Emit(14);
        }

        public void OnCheckpoint(Checkpoint checkpoint)
        {
            if (!racing || checkpoint.index != nextCheckpoint) return;
            nextCheckpoint++;
            respawnPoint = checkpoint.transform;
            ShowMessage("CHECKPOINT!", 1.2f);
        }

        public void OnHazard()
        {
            if (!racing) return;
            falls++;
            ShowMessage("SPLAT! Back to checkpoint", 1.4f);
            ResetRaft(respawnPoint);
        }

        public void OnFinish()
        {
            if (!racing) return;
            if (nextCheckpoint < checkpointCount)
            {
                ShowMessage("You missed a checkpoint!", 1.5f);
                return;
            }

            racing = false;
            SetLocked(true);

            string key = "best_" + playerCount + (solo ? "_solo" : "");
            float best = PlayerPrefs.GetFloat(key, float.MaxValue);
            bool record = raceTime < best;
            if (record)
            {
                best = raceTime;
                PlayerPrefs.SetFloat(key, best);
                PlayerPrefs.Save();
            }

            finishText.text = "FINISH!\nTime " + FormatTime(raceTime) + "   Falls " + falls + "\n" +
                              (record ? "NEW BEST!" : "Best " + FormatTime(best));
            finishPanel.SetActive(true);
        }

        void ShowMessage(string text, float duration)
        {
            messageText.text = text;
            messageText.enabled = true;
            messageHideTime = Time.time + duration;
        }

        void UpdateHud()
        {
            timerText.text = FormatTime(raceTime);
            if (fallsText != null) fallsText.text = "FALLS " + falls;
            if (progressFill != null && coursePath != null && coursePath.Length > 1)
            {
                progressIndex = CoursePath.FindNearest(coursePath, raft.transform.position, progressIndex);
                progressFill.fillAmount = (float)progressIndex / (coursePath.Length - 1);
            }
        }

        static string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            return minutes + ":" + (seconds - minutes * 60f).ToString("00.00");
        }
    }
}
