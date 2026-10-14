using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FartRaft
{
    public enum Difficulty { Easy, Hard }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static float ObstacleSpeed = 1f;

        // Player count to start with right after a stage scene loads (0 = show the menu).
        static int pendingPlayers;

        public static string StageSceneName(int index) => "Stage" + (index + 1);

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
        public Button difficultyButton;
        public Button nextStageButton;
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
        public Text[] nameTags = new Text[4];
        public Image progressFill;
        public Image flash;

        [Header("Lobby")]
        public Transform lobbyView;
        public LobbyAvatar[] lobbyAvatars = new LobbyAvatar[4];
        public Button[] joinButtons = new Button[4];
        public Text[] joinTexts = new Text[4];
        public GameObject[] joinedMarks = new GameObject[4];
        public GameObject[] pickers = new GameObject[4];
        public Text[] characterNames = new Text[4];
        public Button[] characterPrevButtons = new Button[4];
        public Button[] characterNextButtons = new Button[4];
        public Button[] stageCards = new Button[0];
        public GameObject[] stageHighlights = new GameObject[0];
        public Button playButton;

        [Header("Settings")]
        public Button settingsButton;
        public GameObject settingsPanel;
        public Button settingsCloseButton;
        public Button musicButton;
        public Text musicText;
        public Button soundButton;
        public Text soundText;

        [Header("Music")]
        public AudioClip lobbyMusic;
        public AudioClip raceMusic;

        [Header("Rules")]
        [Tooltip("Thrust multiplier while every player farts at the same time.")]
        public float syncBoost = 1.4f;
        [Tooltip("How far below the slide surface the raft can drop before it counts as a fall.")]
        public float fallDepth = 5f;
        [Tooltip("Thrust multiplier while the BEANS power-up is active.")]
        public float beansBoost = 1.8f;
        public float beansTime = 4f;
        public Difficulty difficulty = Difficulty.Easy;
        [Tooltip("Easy mode: after an OUCH the raft blinks and passes through spiky things for this long.")]
        public float easySafeTime = 2f;
        [Tooltip("Easy mode: a spiky hit after the blinking but within this many seconds of the OUCH kills.")]
        public float easyHitWindow = 4f;
        public float easyHitBounce = 3f;

        [Header("Stages")]
        public int stageIndex;
        public string[] stageNames = { "STAGE 1" };

        static readonly float[][] SeatAngles =
        {
            new[] { 180f },
            new[] { 135f, 225f },
            new[] { 180f, 60f, 300f },
            new[] { 135f, 225f, 315f, 45f },
        };
        static readonly float[] PowerPerPlayer = { 1f, 0.8f, 0.7f, 0.62f };

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
        bool ghostActive;
        float beansUntil = -100f;
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

            playAgainButton.onClick.AddListener(() => StartGame(playerCount));
            finishMenuButton.onClick.AddListener(ShowMenu);
            hudMenuButton.onClick.AddListener(ShowMenu);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(ToggleDifficulty);
            if (nextStageButton != null) nextStageButton.onClick.AddListener(() => LoadStage(stageIndex + 1, playerCount));

            difficulty = (Difficulty)PlayerPrefs.GetInt(DifficultyKey, (int)difficulty);
            ApplyDifficulty();

            playerCount = GameSettings.Players;
            for (int i = 0; i < seats.Length; i++)
            {
                int player = i;
                if (Has(characterPrevButtons, i)) characterPrevButtons[i].onClick.AddListener(() => CycleCharacter(player, -1));
                if (Has(characterNextButtons, i)) characterNextButtons[i].onClick.AddListener(() => CycleCharacter(player, 1));
                if (Has(joinButtons, i)) joinButtons[i].onClick.AddListener(() => ToggleJoin(player));
                int pick = CharacterRoster.Load(i);
                if (!seats[i].HasCharacter(pick)) pick = CharacterRoster.Fallback(i);
                SetCharacter(i, pick);
            }
            for (int i = 0; i < stageCards.Length; i++)
            {
                int stage = i;
                if (stageCards[i] != null && i < stageNames.Length) stageCards[i].onClick.AddListener(() => PickStage(stage));
            }
            if (playButton != null) playButton.onClick.AddListener(() => StartGame(playerCount));

            if (settingsButton != null) settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(() => settingsPanel.SetActive(false));
            if (musicButton != null) musicButton.onClick.AddListener(() => { GameSettings.MusicOn = !GameSettings.MusicOn; UpdateSettingsLabels(); });
            if (soundButton != null) soundButton.onClick.AddListener(() => { GameSettings.SoundOn = !GameSettings.SoundOn; UpdateSettingsLabels(); });
            if (settingsPanel != null) settingsPanel.SetActive(false);
            UpdateSettingsLabels();
            UpdateLobby();
        }

        static bool Has<T>(T[] items, int i) where T : Object => items != null && i < items.Length && items[i] != null;

        void SetCharacter(int player, int index)
        {
            seats[player].SetCharacter(index);
            if (Has(lobbyAvatars, player)) lobbyAvatars[player].SetCharacter(index);
            string name = seats[player].CharacterName;
            if (Has(characterNames, player)) characterNames[player].text = name;
            if (Has(nameTags, player)) nameTags[player].text = name;
            if (Has(joysticks, player)) joysticks[player].SetLabel(name);
        }

        void CycleCharacter(int player, int direction)
        {
            var seat = seats[player];
            int count = CharacterRoster.All.Length;
            int index = seat.CharacterIndex;
            for (int step = 0; step < count; step++)
            {
                index = (index + direction + count) % count;
                if (seat.HasCharacter(index)) break;
            }
            SetCharacter(player, index);
            CharacterRoster.Save(player, index);
            if (Has(lobbyAvatars, player)) lobbyAvatars[player].Cheer();
        }

        // Slots fill from the left: tapping an empty slot joins everyone up to it, tapping the last player removes them.
        void ToggleJoin(int player)
        {
            int count;
            if (player >= playerCount) count = player + 1;
            else if (player > 0 && player == playerCount - 1) count = player;
            else return;
            bool joined = count > playerCount;
            playerCount = count;
            GameSettings.Players = count;
            UpdateLobby();
            if (joined && Has(lobbyAvatars, player)) lobbyAvatars[player].Cheer();
        }

        void UpdateLobby()
        {
            for (int i = 0; i < seats.Length; i++)
            {
                bool joined = i < playerCount;
                if (Has(lobbyAvatars, i)) lobbyAvatars[i].gameObject.SetActive(joined);
                if (Has(pickers, i)) pickers[i].SetActive(joined);
                if (Has(joinedMarks, i)) joinedMarks[i].SetActive(joined);
                if (Has(joinTexts, i)) joinTexts[i].text = joined ? (i == 0 ? "YOU" : "PLAYER " + (i + 1)) : "+ JOIN";
            }
            for (int i = 0; i < stageCards.Length; i++)
            {
                if (stageCards[i] == null) continue;
                if (Has(stageHighlights, i)) stageHighlights[i].SetActive(i == stageIndex);
                stageCards[i].transform.localScale = Vector3.one * (i == stageIndex ? 1.06f : 1f);
            }
        }

        void PickStage(int index)
        {
            if (index == stageIndex) return;
            LoadStage(index, 0);
        }

        void UpdateSettingsLabels()
        {
            if (musicText != null) musicText.text = GameSettings.MusicOn ? "MUSIC: ON" : "MUSIC: OFF";
            if (soundText != null) soundText.text = GameSettings.SoundOn ? "SOUND: ON" : "SOUND: OFF";
            if (musicText != null) musicText.color = GameSettings.MusicOn ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.45f, 0.35f);
            if (soundText != null) soundText.color = GameSettings.SoundOn ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.45f, 0.35f);
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
            raft.sideDrag = easy ? 3.5f : 0f;

            foreach (var seat in seats)
            {
                seat.thrust = easy ? 9f : 3f;
                seat.startKick = 0.8f;
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
            if (pendingPlayers > 0)
            {
                int count = pendingPlayers;
                pendingPlayers = 0;
                StartGame(count);
            }
            else
            {
                ShowMenu();
            }
        }

        string StageName => stageIndex < stageNames.Length ? stageNames[stageIndex] : "STAGE " + (stageIndex + 1);
        bool HasNextStage => stageIndex + 1 < stageNames.Length;

        void LoadStage(int index, int startWithPlayers)
        {
            pendingPlayers = startWithPlayers;
            SceneManager.LoadScene(StageSceneName(index));
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
            UpdateLobby();
            if (cameraFollow != null) cameraFollow.fixedView = lobbyView;
            MusicPlayer.Instance.Play(lobbyMusic);
        }

        public void StartGame(int count)
        {
            StopRoutine();
            playerCount = Mathf.Clamp(count, 1, 4);
            ConfigureSeats();

            menuPanel.SetActive(false);
            finishPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            hudPanel.SetActive(true);
            if (cameraFollow != null) cameraFollow.fixedView = null;
            MusicPlayer.Instance.Play(raceMusic);

            racing = false;
            dying = false;
            raceTime = 0f;
            deaths = 0;
            nextCheckpoint = 0;
            lastWarningTime = -100f;
            beansUntil = -100f;
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
                joysticks[i].SetLabel(seats[i].CharacterName);
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
            float boost = (syncActive ? syncBoost : 1f) * (racing && Time.time < beansUntil ? beansBoost : 1f);
            foreach (var seat in seats) seat.BoostMultiplier = boost;

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

            SetGhost(racing && !dying && Time.time - lastWarningTime < easySafeTime);

            if (racing && coursePath != null && coursePath.Length > 1)
            {
                MusicPlayer.Instance.SetTempo((float)Mathf.Max(progressIndex, 0) / (coursePath.Length - 1) > 0.85f ? 1.08f : 1f);
            }

            UpdateHud();
            syncText.enabled = syncActive;
            if (hintText != null) hintText.enabled = raceTime < 8f;
            if (messageText.enabled && Time.time > messageHideTime) messageText.enabled = false;
        }

        void SetGhost(bool ghost)
        {
            if (ghost == ghostActive) return;
            ghostActive = ghost;
            Physics.IgnoreLayerCollision(raft.gameObject.layer, Hazard.Layer, ghost);
            raft.SetBlinking(ghost);
        }

        void OnDestroy()
        {
            Physics.IgnoreLayerCollision(raft.gameObject.layer, Hazard.Layer, false);
        }

        public void OnHazard(Vector3 point)
        {
            if (!racing || dying) return;

            if (difficulty == Difficulty.Easy)
            {
                float since = Time.time - lastWarningTime;
                if (since < easySafeTime) return;
                if (since > easyHitWindow)
                {
                    lastWarningTime = Time.time;
                    raft.Bounce(point, easyHitBounce);
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

        public bool OnBeans(Vector3 position)
        {
            if (!racing || dying) return false;
            beansUntil = Time.time + beansTime;
            foreach (var seat in seats) seat.ResetGas();
            PlayImpact(position, 12);
            Audio.Checkpoint();
            ShowMessage("BEANS! MEGA FART!", 1.5f);
            return true;
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
            MusicPlayer.Instance.Stop();
            foreach (var seat in seats)
            {
                if (seat.gameObject.activeSelf) seat.Cheer();
            }

            string key = "best_v3_" + StageSceneName(stageIndex) + "_" + difficulty + "_" + playerCount;
            float best = PlayerPrefs.GetFloat(key, float.MaxValue);
            bool record = raceTime < best;
            if (record)
            {
                best = raceTime;
                PlayerPrefs.SetFloat(key, best);
                PlayerPrefs.Save();
            }

            finishText.text = (HasNextStage ? "STAGE " + (stageIndex + 1) + " DONE!" : "YOU BEAT ALL STAGES!") +
                              "\nTime " + FormatTime(raceTime) + "   Deaths " + deaths + "\n" +
                              (record ? "NEW BEST!" : "Best " + FormatTime(best));
            if (nextStageButton != null) nextStageButton.gameObject.SetActive(HasNextStage);
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
