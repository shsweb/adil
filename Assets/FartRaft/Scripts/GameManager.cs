using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FartRaft
{
    public enum Difficulty { Easy, Hard }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static float ObstacleSpeed = 1f;

        [Header("Scene")]
        public Raft raft;
        public FartSeat[] seats = new FartSeat[4];
        public FartJoystick[] joysticks = new FartJoystick[4];
        public Transform startPoint;
        public CameraFollow cameraFollow;
        public Vector3[] coursePath;
        public int checkpointCount = 4;
        public ParticleSystem impactEffect;
        public GameAudio gameAudio;

        [Header("UI")]
        public GameObject menuPanel;
        public GameObject hudPanel;
        public GameObject finishPanel;
        public Button soloButton;
        public Button twoPlayersButton;
        public Button threePlayersButton;
        public Button fourPlayersButton;
        public Button difficultyButton;
        public Text difficultyText;
        public Button playAgainButton;
        public Button finishMenuButton;
        public Button hudMenuButton;
        public Text timerText;
        public Text messageText;
        public Text syncText;
        public Text finishText;
        public Text deathsText;
        public Text hintText;
        public Image progressFill;
        public Image flash;

        [Header("Rules")]
        [Tooltip("Thrust multiplier while every player farts at the same time.")]
        public float syncBoost = 1.4f;
        [Tooltip("How far below the slide surface the raft can drop before it counts as a fall.")]
        public float fallDepth = 5f;
        public Difficulty difficulty = Difficulty.Easy;
        [Tooltip("Easy mode: right after an OUCH you are safe for this long (hits only bounce).")]
        public float easySafeTime = 1.5f;
        [Tooltip("Easy mode: a second spiky hit within this many seconds of an OUCH kills.")]
        public float easyHitWindow = 3.5f;
        public float easyHitBounce = 9f;

        static readonly float[][] SeatAngles =
        {
            new[] { 180f },
            new[] { 135f, 225f },
            new[] { 180f, 60f, 300f },
            new[] { 135f, 225f, 315f, 45f },
        };
        static readonly float[] PowerPerPlayer = { 1f, 0.8f, 0.7f, 0.62f };
        static readonly string[] AnimalNames = { "CAPY", "HIPPO", "CROC", "GORILLA" };

        int playerCount = 2;
        bool racing;
        bool dying;
        bool syncActive;
        float raceTime;
        int deaths;
        int nextCheckpoint;
        int progressIndex = -1;
        Transform respawnPoint;
        float messageHideTime;
        float lastWarningTime = -100f;
        Coroutine routine;

        const string DifficultyKey = "difficulty";

        public GameAudio Audio
        {
            get
            {
                if (gameAudio == null) gameAudio = GetComponent<GameAudio>();
                if (gameAudio == null) gameAudio = gameObject.AddComponent<GameAudio>();
                return gameAudio;
            }
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 60f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            soloButton.onClick.AddListener(() => StartGame(1));
            twoPlayersButton.onClick.AddListener(() => StartGame(2));
            threePlayersButton.onClick.AddListener(() => StartGame(3));
            fourPlayersButton.onClick.AddListener(() => StartGame(4));
            playAgainButton.onClick.AddListener(() => StartGame(playerCount));
            finishMenuButton.onClick.AddListener(ShowMenu);
            hudMenuButton.onClick.AddListener(ShowMenu);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(ToggleDifficulty);

            difficulty = (Difficulty)PlayerPrefs.GetInt(DifficultyKey, (int)difficulty);
            ApplyDifficulty();
        }

        void ToggleDifficulty()
        {
            difficulty = difficulty == Difficulty.Easy ? Difficulty.Hard : Difficulty.Easy;
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
            PlayerPrefs.Save();
            ApplyDifficulty();
        }

        void ApplyDifficulty()
        {
            bool easy = difficulty == Difficulty.Easy;

            raft.linearDrag = easy ? 1.2f : 0.18f;
            raft.quadraticDrag = easy ? 0f : 0.012f;
            raft.autoForward = easy ? 6f : 0f;
            raft.maxSpeed = easy ? 11f : 0f;
            raft.lockYaw = easy;
            raft.sideDrag = easy ? 2.5f : 0f;

            foreach (var seat in seats)
            {
                seat.thrust = easy ? 9f : 3f;
                seat.startKick = easy ? 1.5f : 0.8f;
                seat.instantAim = easy;
                seat.drainPerSecond = easy ? 12f : 22f;
            }

            ObstacleSpeed = easy ? 0.6f : 1f;

            if (difficultyText != null)
            {
                difficultyText.text = easy ? "MODE: EASY" : "MODE: HARD";
                difficultyText.color = easy ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.45f, 0.35f);
            }
        }

        void Start()
        {
            ShowMenu();
        }

        public void ShowMenu()
        {
            StopRoutine();
            racing = false;
            dying = false;
            SetFlash(0f);
            SetLocked(true);
            menuPanel.SetActive(true);
            hudPanel.SetActive(false);
            finishPanel.SetActive(false);
            ConfigureSeats();
            ResetRaft(startPoint);
        }

        public void StartGame(int count)
        {
            StopRoutine();
            playerCount = Mathf.Clamp(count, 1, 4);
            ConfigureSeats();

            menuPanel.SetActive(false);
            finishPanel.SetActive(false);
            hudPanel.SetActive(true);

            racing = false;
            dying = false;
            raceTime = 0f;
            deaths = 0;
            nextCheckpoint = 0;
            lastWarningTime = -100f;
            respawnPoint = startPoint;
            SetFlash(0f);
            SetLocked(true);
            ResetRaft(startPoint);
            UpdateHud();
            routine = StartCoroutine(Countdown());
        }

        IEnumerator Countdown()
        {
            for (int i = 3; i > 0; i--)
            {
                ShowMessage(i.ToString(), 1.1f);
                yield return new WaitForSeconds(0.8f);
            }
            ShowMessage("FART!", 1f);
            SetLocked(false);
            racing = true;
            routine = null;
        }

        void StopRoutine()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

        void ConfigureSeats()
        {
            float[] angles = SeatAngles[playerCount - 1];
            for (int i = 0; i < seats.Length; i++)
            {
                bool active = i < playerCount;
                seats[i].gameObject.SetActive(active);
                joysticks[i].gameObject.SetActive(active);
                if (!active) continue;

                seats[i].SetAngle(angles[i]);
                seats[i].thrustScale = PowerPerPlayer[playerCount - 1];
                seats[i].ResetGas();
                joysticks[i].SetLabel(AnimalNames[i]);
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

            if (coursePath != null && coursePath.Length > 1)
            {
                raft.TrackForward = CoursePath.FlatDirection(coursePath, Mathf.Max(progressIndex, 0), 6);
            }
        }

        void Update()
        {
            if (racing) raceTime += Time.deltaTime;

            UpdateProgress();
            if (racing && !dying && coursePath != null && coursePath.Length > 0 &&
                raft.transform.position.y < coursePath[progressIndex].y - fallDepth)
            {
                routine = StartCoroutine(Die(raft.transform.position, true));
            }

            UpdateHud();
            syncText.enabled = syncActive;
            if (hintText != null) hintText.enabled = raceTime < 8f;
            if (messageText.enabled && Time.time > messageHideTime) messageText.enabled = false;
        }

        public void OnHazard(Vector3 point)
        {
            if (!racing || dying) return;

            if (difficulty == Difficulty.Easy)
            {
                float since = Time.time - lastWarningTime;
                if (since < easySafeTime)
                {
                    raft.Bounce(point, easyHitBounce * 0.5f);
                    return;
                }
                if (since > easyHitWindow)
                {
                    lastWarningTime = Time.time;
                    raft.Bounce(point, easyHitBounce);
                    raft.PauseAutoForward(1f);
                    PlayImpact(point, 20);
                    Audio.Bump(10f);
                    ShowMessage("OUCH!", 1f);
                    return;
                }
            }

            routine = StartCoroutine(Die(point, false));
        }

        IEnumerator Die(Vector3 point, bool fell)
        {
            dying = true;
            deaths++;

            if (fell)
            {
                Audio.Fall();
                ShowMessage("WHOOPS!", 1.5f);
                foreach (var seat in seats) seat.locked = true;
                yield return new WaitForSeconds(0.9f);
            }
            else
            {
                Audio.Crash();
                PlayImpact(point, 40);
                ShowMessage("SPLAT!", 1.5f);
                SetLocked(true);
                yield return new WaitForSeconds(0.6f);
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
            {
                SetFlash(t);
                yield return null;
            }
            SetFlash(1f);
            SetLocked(true);
            ResetRaft(respawnPoint);
            yield return new WaitForSeconds(0.2f);

            for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.35f)
            {
                SetFlash(t);
                yield return null;
            }
            SetFlash(0f);

            SetLocked(false);
            ShowMessage("GO!", 0.7f);
            dying = false;
            routine = null;
        }

        void SetFlash(float alpha)
        {
            if (flash == null) return;
            flash.enabled = alpha > 0f;
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        public void PlayImpact(Vector3 position, int count)
        {
            if (impactEffect == null) return;
            impactEffect.transform.position = position + Vector3.up * 0.8f;
            impactEffect.Emit(count);
        }

        public void OnCheckpoint(Checkpoint checkpoint)
        {
            if (!racing || dying || checkpoint.index != nextCheckpoint) return;
            nextCheckpoint++;
            respawnPoint = checkpoint.transform;
            Audio.Checkpoint();
            ShowMessage("CHECKPOINT!", 1.2f);
        }

        public void OnFinish()
        {
            if (!racing || dying) return;
            if (nextCheckpoint < checkpointCount)
            {
                ShowMessage("You missed a checkpoint!", 1.5f);
                return;
            }

            racing = false;
            SetLocked(true);
            Audio.Cheer();

            string key = "best_v3_" + difficulty + "_" + playerCount;
            float best = PlayerPrefs.GetFloat(key, float.MaxValue);
            bool record = raceTime < best;
            if (record)
            {
                best = raceTime;
                PlayerPrefs.SetFloat(key, best);
                PlayerPrefs.Save();
            }

            finishText.text = "FINISH!\nTime " + FormatTime(raceTime) + "   Deaths " + deaths + "\n" +
                              (record ? "NEW BEST!" : "Best " + FormatTime(best));
            finishPanel.SetActive(true);
        }

        void ShowMessage(string text, float duration)
        {
            messageText.text = text;
            messageText.enabled = true;
            messageHideTime = Time.time + duration;
        }

        void UpdateProgress()
        {
            if (coursePath == null || coursePath.Length == 0) return;
            progressIndex = CoursePath.FindNearest(coursePath, raft.transform.position, progressIndex);
        }

        void UpdateHud()
        {
            timerText.text = FormatTime(raceTime);
            if (deathsText != null) deathsText.text = "DEATHS " + deaths;
            if (progressFill != null && coursePath != null && coursePath.Length > 1)
            {
                progressFill.fillAmount = (float)Mathf.Max(progressIndex, 0) / (coursePath.Length - 1);
            }
        }

        static string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            return minutes + ":" + (seconds - minutes * 60f).ToString("00.00");
        }
    }
}
