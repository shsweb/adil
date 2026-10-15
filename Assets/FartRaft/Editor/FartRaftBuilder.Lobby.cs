using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FartRaft.EditorTools
{
    // Lobby like Fart Together: the animals stand in a row in a little 3D set with a name tag over each
    // one and a < > picker under it, stage cards along the bottom, PLAY and SETTINGS buttons.
    public static partial class FartRaftBuilder
    {
        // Far away from the course so the lobby set never shows up during a race.
        static readonly Vector3 LobbyOrigin = new Vector3(0f, 600f, 0f);
        const float LobbyCharacterScale = 1.45f;
        const float LobbySpacing = 2.4f;

        static readonly string[] ComingSoonStages = { "ICE WORLD", "SEWER SPLASH" };

        static Transform[] lobbySlots;

        static void BuildLobby(Mats m, System.Random random, GameManager manager)
        {
            var root = new GameObject("Lobby").transform;
            root.position = LobbyOrigin;

            var ground = Lit(stagePrefix + "LobbyGround", Color.Lerp(stage.grass, new Color(0.93f, 0.78f, 0.52f), 0.55f), 0.05f);
            var wall = Lit(stagePrefix + "LobbyWall", Color.Lerp(stage.coral, stage.slideBody, 0.35f), 0.05f);
            var wallDark = Lit(stagePrefix + "LobbyWallDark", Color.Lerp(stage.coral, Color.black, 0.25f), 0.05f);

            var floor = Prim(PrimitiveType.Cylinder, "Ground", root, Vector3.down * 0.5f, new Vector3(60f, 0.5f, 60f), ground, false);
            NoShadows(floor);

            // Canyon walls in an arc behind the animals.
            for (int i = 0; i < 15; i++)
            {
                float a = Mathf.Lerp(-80f, 80f, i / 14f) * Mathf.Deg2Rad;
                float radius = 13f + R(random) * 3f;
                float h = 9f + R(random) * 7f;
                var block = Prim(PrimitiveType.Cube, "Wall", root, new Vector3(Mathf.Sin(a) * radius, h * 0.5f - 0.5f, Mathf.Cos(a) * radius),
                    new Vector3(6.5f + R(random) * 2f, h, 3f + R(random) * 2f), i % 3 == 0 ? wallDark : wall, false);
                block.transform.localRotation = Quaternion.Euler(R(random) * 6f - 3f, a * Mathf.Rad2Deg + R(random) * 16f - 8f, R(random) * 6f - 3f);
            }

            // Plants and rocks around the edges, kept away from the middle so they never hide an animal.
            for (int i = 0; i < 26; i++)
            {
                float a = Mathf.Lerp(-95f, 95f, R(random)) * Mathf.Deg2Rad;
                float radius = 5f + R(random) * 7f;
                var pos = root.position + new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                if (Mathf.Abs(pos.x - root.position.x) < 5f && pos.z - root.position.z < 4f) continue;
                float pick = R(random);
                bool placed = pick < 0.45f ? TrySpawnFitted(props.bushes, root, pos, 1f + R(random), true, random)
                    : pick < 0.7f ? TrySpawnFitted(props.flowers, root, pos, 0.6f + R(random) * 0.4f, true, random)
                    : pick < 0.85f ? TrySpawnFitted(props.rocks, root, pos, 0.8f + R(random) * 1.2f, false, random)
                    : TrySpawnFitted(props.trees, root, pos + Vector3.forward * 3f, 4f + R(random) * 2f, true, random);
                if (!placed)
                {
                    var rock = Prim(PrimitiveType.Sphere, "Rock", root, pos - root.position, new Vector3(1.2f, 0.7f, 1f) * (0.6f + R(random)), m.rock, false);
                    rock.transform.localRotation = Quaternion.Euler(0f, R(random) * 360f, 0f);
                }
            }

            var view = new GameObject("LobbyCamera").transform;
            view.SetParent(root, false);
            view.localPosition = new Vector3(0f, 2.3f, -6.4f);
            view.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.45f, 0f) - view.localPosition);
            manager.lobbyView = view;

            lobbySlots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var slot = new GameObject("Slot_P" + (i + 1)).transform;
                slot.SetParent(root, false);
                slot.localPosition = new Vector3((i - 1.5f) * LobbySpacing, 0f, 0f);
                lobbySlots[i] = slot;
                manager.lobbyAvatars[i] = BuildLobbyAvatar(m, slot, i);
            }
        }

        static LobbyAvatar BuildLobbyAvatar(Mats m, Transform slot, int index)
        {
            var go = new GameObject("Avatar");
            go.transform.SetParent(slot, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            var scaled = new GameObject("Scaled").transform;
            scaled.SetParent(body, false);
            scaled.localScale = Vector3.one * LobbyCharacterScale;
            var skins = BuildSkins(scaled, m);
            int pick = CharacterRoster.Default(index);
            if (skins[pick] == null) pick = CharacterRoster.Fallback(index);
            skins[pick].gameObject.SetActive(true);

            var puffGo = new GameObject("FartParticles");
            puffGo.transform.SetParent(scaled, false);
            puffGo.transform.localPosition = new Vector3(0f, 0.4f, -0.45f);
            puffGo.transform.localRotation = Quaternion.Euler(-10f, 180f, 0f);
            var puff = BuildFartParticles(puffGo, m.puff);

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var avatar = go.AddComponent<LobbyAvatar>();
            avatar.skins = skins;
            avatar.body = body;
            avatar.puff = puff;
            avatar.fartAudio = audio;
            avatar.fartClips = FartClips();
            return avatar;
        }

        static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rt = UIObject(name, parent);
            Place(rt, anchor, pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return rt;
        }

        static void BuildLobbyUI(Transform root, GameManager manager)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            var lobby = UIObject("Lobby", root);
            Stretch(lobby);
            manager.menuPanel = lobby.gameObject;

            var logo = Label(lobby, "Logo", "FART RAFT", 92, new Vector2(0f, 1f), new Vector2(330f, -75f), new Vector2(620f, 120f));
            logo.color = new Color(0.75f, 1f, 0.25f);
            var logoOutline = logo.GetComponent<Outline>();
            logoOutline.effectColor = new Color(0.1f, 0.25f, 0.05f, 0.9f);
            logoOutline.effectDistance = new Vector2(5f, -5f);

            var banner = Box(lobby, "Banner", new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(420f, 100f), new Color(0.16f, 0.17f, 0.26f, 0.96f));
            Label(banner, "Text", "LOBBY", 62, center, Vector2.zero, new Vector2(420f, 100f));

            manager.settingsButton = MakeButton(lobby, "Settings", "SETTINGS", new Vector2(1f, 1f), new Vector2(-180f, -65f), new Vector2(300f, 100f), new Color(0.16f, 0.17f, 0.26f, 0.96f), 40);

            for (int i = 0; i < 4; i++)
            {
                Color pc = PlayerColors[i];
                Color dark = new Color(pc.r * 0.5f, pc.g * 0.5f, pc.b * 0.55f, 0.95f);

                // Name tag over the head; tap it to join or leave.
                var tag = Box(lobby, "Tag_P" + (i + 1), center, Vector2.zero, new Vector2(300f, 76f), dark);
                var follow = tag.gameObject.AddComponent<WorldLabel>();
                follow.target = lobbySlots[i];
                follow.offset = new Vector3(0f, 1.45f * LobbyCharacterScale + 0.45f, 0f);
                var tagButton = tag.gameObject.AddComponent<Button>();
                tagButton.targetGraphic = tag.GetComponent<Image>();
                manager.joinButtons[i] = tagButton;
                manager.joinTexts[i] = Label(tag, "Text", "PLAYER " + (i + 1), 40, center, Vector2.zero, new Vector2(300f, 76f));

                var mark = UIObject("Joined", tag);
                Place(mark, center, new Vector2(-150f, 0f), new Vector2(68f, 68f));
                var circle = mark.gameObject.AddComponent<Image>();
                circle.sprite = circleSprite;
                circle.color = new Color(0.3f, 0.78f, 0.3f);
                circle.raycastTarget = false;
                Tick(mark);
                manager.joinedMarks[i] = mark.gameObject;

                // < NAME > under the feet.
                var picker = UIObject("Picker_P" + (i + 1), lobby);
                Place(picker, center, Vector2.zero, new Vector2(330f, 100f));
                var pickerImage = picker.gameObject.AddComponent<Image>();
                pickerImage.color = new Color(0f, 0f, 0f, 0f);
                var pickerFollow = picker.gameObject.AddComponent<WorldLabel>();
                pickerFollow.target = lobbySlots[i];
                pickerFollow.offset = new Vector3(0f, -0.45f, 0f);
                var nameBack = Box(picker, "NameBack", center, Vector2.zero, new Vector2(170f, 70f), new Color(1f, 1f, 1f, 0.92f));
                var name = Label(nameBack, "Name", "", 38, center, Vector2.zero, new Vector2(160f, 66f));
                name.color = dark;
                FitText(name, 20, 38);
                Object.DestroyImmediate(name.GetComponent<Outline>());
                manager.characterNames[i] = name;
                manager.characterPrevButtons[i] = MakeButton(picker, "Prev", "<", center, new Vector2(-126f, 0f), new Vector2(74f, 96f), pc, 60);
                manager.characterNextButtons[i] = MakeButton(picker, "Next", ">", center, new Vector2(126f, 0f), new Vector2(74f, 96f), pc, 60);
                manager.pickers[i] = picker.gameObject;
            }

            // Stage cards.
            var tab = Box(lobby, "ChooseStage", center, new Vector2(-560f, -238f), new Vector2(460f, 82f), new Color(0.55f, 0.45f, 0.92f));
            Label(tab, "Text", "CHOOSE STAGE", 46, center, Vector2.zero, new Vector2(460f, 82f));
            int total = Stages.Length + ComingSoonStages.Length;
            const float cardW = 250f, cardH = 170f, gap = 16f;
            float rowW = total * cardW + (total - 1) * gap;
            float rowX = -170f;
            Box(lobby, "CardsBack", center, new Vector2(rowX, -370f), new Vector2(rowW + 34f, cardH + 34f), new Color(1f, 1f, 1f, 0.95f));
            manager.stageCards = new Button[Stages.Length];
            manager.stageHighlights = new GameObject[Stages.Length];
            for (int k = 0; k < total; k++)
            {
                bool real = k < Stages.Length;
                var pos = new Vector2(rowX - rowW * 0.5f + cardW * 0.5f + k * (cardW + gap), -370f);
                var card = UIObject("Card_" + (k + 1), lobby);
                Place(card, center, pos, new Vector2(cardW, cardH));

                var highlight = real ? Box(card, "Highlight", center, Vector2.zero, new Vector2(cardW + 16f, cardH + 16f), new Color(0.95f, 0.25f, 0.55f)) : null;
                Color face = real ? Color.Lerp(Stages[k].skyTint, Stages[k].slideBody, 0.5f) : new Color(0.45f, 0.45f, 0.48f);
                var faceRt = Box(card, "Face", center, Vector2.zero, new Vector2(cardW, cardH), face);
                var stripe = Box(faceRt, "Stripe", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(cardW - 16f, 64f), new Color(0.1f, 0.1f, 0.15f, 0.75f));
                var title = Label(stripe, "Title", real ? Stages[k].name : ComingSoonStages[k - Stages.Length], 30, center, Vector2.zero, new Vector2(cardW - 24f, 60f));
                title.color = real ? new Color(1f, 0.92f, 0.3f) : new Color(0.8f, 0.8f, 0.8f);
                FitText(title, 16, 30);
                Label(faceRt, "Number", real ? "STAGE " + (k + 1) : "COMING SOON", real ? 34 : 26, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(cardW - 20f, 50f));

                if (!real) continue;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = faceRt.GetComponent<Image>();
                manager.stageCards[k] = button;
                manager.stageHighlights[k] = highlight.gameObject;
                highlight.gameObject.SetActive(k == 0);
            }

            manager.playButton = MakeButton(lobby, "Play", "PLAY!", new Vector2(1f, 0f), new Vector2(-220f, 140f), new Vector2(360f, 200f), new Color(0.35f, 0.8f, 0.25f), 86);
            var hint = Label(lobby, "Hint", "Tap + JOIN to add friends - everyone plays on this phone!", 28, center, new Vector2(150f, -238f), new Vector2(760f, 50f));
            hint.alignment = TextAnchor.MiddleLeft;
        }

        // Shrinks the text until it fits its box on one or two lines.
        static void FitText(Text text, int min, int max)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = max;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // Check mark from two white bars.
        static void Tick(Transform parent)
        {
            var shortBar = UIObject("TickShort", parent);
            Place(shortBar, new Vector2(0.5f, 0.5f), new Vector2(-11f, -4f), new Vector2(10f, 26f));
            shortBar.localRotation = Quaternion.Euler(0f, 0f, 45f);
            shortBar.gameObject.AddComponent<Image>().raycastTarget = false;
            var longBar = UIObject("TickLong", parent);
            Place(longBar, new Vector2(0.5f, 0.5f), new Vector2(6f, 3f), new Vector2(10f, 44f));
            longBar.localRotation = Quaternion.Euler(0f, 0f, -38f);
            longBar.gameObject.AddComponent<Image>().raycastTarget = false;
        }

        static void BuildSettingsUI(Transform root, GameManager manager)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            var panel = Panel(root, "Settings", new Color(0f, 0f, 0f, 0.6f));
            manager.settingsPanel = panel.gameObject;
            var box = Box(panel, "Box", center, Vector2.zero, new Vector2(820f, 700f), new Color(0.16f, 0.17f, 0.26f, 0.98f));
            var title = Label(box, "Title", "SETTINGS", 76, center, new Vector2(0f, 255f), new Vector2(700f, 110f));
            title.color = new Color(0.75f, 1f, 0.25f);

            var button = new Color(0.27f, 0.3f, 0.42f);
            manager.difficultyButton = MakeButton(box, "Mode", "MODE: EASY", center, new Vector2(0f, 120f), new Vector2(600f, 110f), button, 46);
            manager.difficultyText = manager.difficultyButton.GetComponentInChildren<Text>();
            manager.musicButton = MakeButton(box, "Music", "MUSIC: ON", center, new Vector2(0f, -10f), new Vector2(600f, 110f), button, 46);
            manager.musicText = manager.musicButton.GetComponentInChildren<Text>();
            manager.soundButton = MakeButton(box, "Sound", "SOUND: ON", center, new Vector2(0f, -140f), new Vector2(600f, 110f), button, 46);
            manager.soundText = manager.soundButton.GetComponentInChildren<Text>();
            Label(box, "ModeHint", "EASY = raft goes by itself.  HARD = like the real game.", 26, center, new Vector2(0f, -215f), new Vector2(760f, 40f));
            manager.settingsCloseButton = MakeButton(box, "Close", "CLOSE", center, new Vector2(0f, -285f), new Vector2(360f, 100f), PlayerColors[2], 46);
            panel.gameObject.SetActive(false);
        }

        static AudioClip LoadMusic(string name)
        {
            string path = AudioFolder + "/Music/" + name + ".ogg";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Fart Raft] Missing music " + path);
                return null;
            }
            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.Streaming)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
