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
        public int checkpointCount = 3;

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

        [Header("Rules")]
        [Tooltip("Thrust multiplier while every player farts at the same time.")]
        public float syncBoost = 1.5f;

        static readonly Vector2[] TwoSeatLayout = { new Vector2(-1.3f, -1.3f), new Vector2(1.3f, -1.3f) };
        static readonly Vector2[] ThreeSeatLayout = { new Vector2(-1.3f, -1.3f), new Vector2(1.3f, -1.3f), new Vector2(0f, 1.3f) };
        static readonly Vector2[] FourSeatLayout = { new Vector2(-1.3f, -1.3f), new Vector2(1.3f, -1.3f), new Vector2(-1.3f, 1.3f), new Vector2(1.3f, 1.3f) };

        int playerCount = 2;
        bool solo;
        bool racing;
        bool syncActive;
        float raceTime;
        int nextCheckpoint;
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
            nextCheckpoint = 0;
            respawnPoint = startPoint;
            ResetRaft(startPoint);
            UpdateTimer();
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
            Vector2[] layout = playerCount == 2 ? TwoSeatLayout : playerCount == 3 ? ThreeSeatLayout : FourSeatLayout;

            for (int i = 0; i < seats.Length; i++)
            {
                bool active = i < playerCount;
                seats[i].gameObject.SetActive(active);
                buttons[i].gameObject.SetActive(active);
                if (!active) continue;

                Vector3 local = seats[i].transform.localPosition;
                seats[i].transform.localPosition = new Vector3(layout[i].x, local.y, layout[i].y);
                // Total thrust stays the same regardless of player count.
                seats[i].thrustScale = 2f / playerCount;
                seats[i].ResetGas();
                buttons[i].SetLabel(solo ? (i == 0 ? "LEFT" : "RIGHT") : "P" + (i + 1));
            }
        }

        void SetLocked(bool locked)
        {
            foreach (var seat in seats) seat.locked = locked;
        }

        void ResetRaft(Transform point)
        {
            raft.Respawn(point.position, point.rotation);
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
                UpdateTimer();
            }

            syncText.enabled = syncActive;
            if (messageText.enabled && Time.time > messageHideTime) messageText.enabled = false;
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

            finishText.text = "FINISH!\nTime " + FormatTime(raceTime) + "\n" + (record ? "NEW BEST!" : "Best " + FormatTime(best));
            finishPanel.SetActive(true);
        }

        void ShowMessage(string text, float duration)
        {
            messageText.text = text;
            messageText.enabled = true;
            messageHideTime = Time.time + duration;
        }

        void UpdateTimer()
        {
            timerText.text = FormatTime(raceTime);
        }

        static string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            return minutes + ":" + (seconds - minutes * 60f).ToString("00.00");
        }
    }
}
