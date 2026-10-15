using System.IO;
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

        static readonly string[] ComingSoonStages = { };

        static Transform[] lobbySlots;

        static void BuildLobby(Mats m, System.Random random, GameManager manager)
        {
            var root = new GameObject("Lobby").transform;
            root.position = LobbyOrigin;

            // Feet stay at y = 0 so the name tags and pickers land in the same screen spots.
            // The pool sits a step below the wooden deck.
            const float grassTop = -0.42f;
            const float sandTop = -0.06f;
            const float waterTop = -0.14f;

            var grass = Lit(stagePrefix + "LobbyGrass", stage.grass, 0.04f);
            var sand = Lit(stagePrefix + "LobbySand", new Color(0.90f, 0.78f, 0.48f), 0.04f);
            var pool = LobbyPoolMaterial(m);

            var floor = Prim(PrimitiveType.Cylinder, "Ground", root, new Vector3(0f, grassTop - 0.5f, 0f), new Vector3(90f, 0.5f, 90f), grass, false);
            NoShadows(floor);
            Prim(PrimitiveType.Cube, "Sand", root, new Vector3(0f, sandTop - 0.15f, 7.2f), new Vector3(22f, 0.3f, 16f), sand, false);
            Prim(PrimitiveType.Cube, "Deck", root, new Vector3(0f, -0.21f, -0.2f), new Vector3(13.4f, 0.42f, 5.1f), m.wood, false);
            for (float z = -1.8f; z <= 1.61f; z += 0.55f)
            {
                Prim(PrimitiveType.Cube, "Plank", root, new Vector3(0f, 0.012f, z), new Vector3(12.6f, 0.02f, 0.05f), m.darkWood, false);
            }

            var water = Prim(PrimitiveType.Cube, "Pool", root, new Vector3(0f, waterTop - 0.04f, 6.85f), new Vector3(12.2f, 0.08f, 8.2f), pool, false);
            water.AddComponent<UVScroll>().speed = new Vector2(0.02f, -0.06f);
            Prim(PrimitiveType.Cube, "RimFront", root, new Vector3(0f, -0.15f, 2.52f), new Vector3(13.4f, 0.3f, 0.42f), m.wood, false);
            Prim(PrimitiveType.Cube, "RimLeft", root, new Vector3(-6.55f, -0.14f, 6.85f), new Vector3(0.5f, 0.34f, 8.8f), m.darkWood, false);
            Prim(PrimitiveType.Cube, "RimRight", root, new Vector3(6.55f, -0.14f, 6.85f), new Vector3(0.5f, 0.34f, 8.8f), m.darkWood, false);
            Prim(PrimitiveType.Cube, "RimBack", root, new Vector3(0f, -0.14f, 11.25f), new Vector3(13.6f, 0.34f, 0.55f), m.darkWood, false);

            var tropical = TropicalPrefabs();
            var palms = Named(tropical, "palm");
            var trees = palms.Length > 0 ? palms : props.trees;
            var plants = Named(tropical, "plant");
            if (plants.Length == 0) plants = props.bushes;
            var rocks = Named(tropical, "rock");
            if (rocks.Length == 0) rocks = props.rocks;

            Vector3[] palmSpots =
            {
                new Vector3(-8.2f, grassTop, -0.4f), new Vector3(8.4f, grassTop, 0.1f),
                new Vector3(-7.6f, grassTop, 6.2f), new Vector3(7.8f, grassTop, 5.8f),
                new Vector3(-3.6f, grassTop, 13.6f), new Vector3(3.4f, grassTop, 13.2f),
                new Vector3(0.2f, grassTop, 15.2f),
                new Vector3(-11.2f, grassTop, 2.4f), new Vector3(11.4f, grassTop, 2.8f),
            };
            int palmsPlaced = 0;
            for (int i = 0; i < palmSpots.Length; i++)
            {
                if (PlaceLobbyProp(trees, root, palmSpots[i], 6.2f + R(random) * 2.4f, true, R(random) * 360f, random)) palmsPlaced++;
                else Tree(root, m, root.TransformPoint(palmSpots[i]), random);
            }

            Vector3[] plantSpots =
            {
                new Vector3(-5.6f, 0f, -1.7f), new Vector3(5.5f, 0f, -1.6f),
                new Vector3(-6.2f, 0f, 1.5f), new Vector3(6.3f, 0f, 1.4f),
                new Vector3(-5.2f, sandTop, 3.4f), new Vector3(5.3f, sandTop, 3.2f),
                new Vector3(-2.2f, sandTop, 11.6f), new Vector3(2.4f, sandTop, 11.5f),
            };
            foreach (var spot in plantSpots)
            {
                if (!PlaceLobbyProp(plants, root, spot, 0.7f + R(random) * 0.5f, true, R(random) * 360f, random))
                    Bush(root, m, root.TransformPoint(spot), random);
            }
            Vector3[] rockSpots =
            {
                new Vector3(-7.4f, sandTop, 3.1f), new Vector3(7.5f, sandTop, 2.8f),
                new Vector3(-7.2f, sandTop, 10.4f), new Vector3(7.3f, sandTop, 10.2f),
                new Vector3(0.8f, sandTop, 12.4f),
            };
            foreach (var spot in rockSpots)
            {
                if (!PlaceLobbyProp(rocks, root, spot, 0.7f + R(random) * 0.8f, false, R(random) * 360f, random))
                {
                    var rock = Prim(PrimitiveType.Sphere, "Rock", root, spot, new Vector3(1.1f, 0.7f, 0.9f) * (0.7f + R(random) * 0.5f), m.rock, false);
                    rock.transform.localRotation = Quaternion.Euler(0f, R(random) * 360f, 0f);
                }
            }

            PlaceLobbyProp(Named(tropical, "tent"), root, new Vector3(-7.8f, grassTop, -1.4f), 3.1f, true, 200f, random);
            PlaceLobbyProp(Named(tropical, "chest"), root, new Vector3(5.7f, 0f, -1.15f), 0.65f, true, 15f, random);
            PlaceLobbyProp(Named(tropical, "barrel"), root, new Vector3(5.15f, 0f, 0.55f), 0.85f, true, 40f, random);
            PlaceLobbyProp(Named(tropical, "barrel"), root, new Vector3(6.15f, 0f, 0.15f), 0.75f, true, 110f, random);
            PlaceLobbyProp(Named(tropical, "pier"), root, new Vector3(-4.4f, waterTop, 6.4f), 7f, false, 90f, random);
            PlaceLobbyProp(props.flowers, root, new Vector3(-4.6f, 0f, 1.7f), 0.55f, true, 0f, random);
            PlaceLobbyProp(props.flowers, root, new Vector3(4.5f, 0f, 1.6f), 0.55f, true, 0f, random);

            var floatMesh = TorusMesh("LobbyFloat", 0.58f, 0.15f, 18, 8);
            Color[] floatColors =
            {
                new Color(1f, 0.38f, 0.58f), new Color(1f, 0.86f, 0.22f),
                new Color(0.30f, 0.85f, 1f), new Color(0.45f, 0.9f, 0.38f),
            };
            Vector3[] floatSpots = { new Vector3(-2.6f, waterTop, 5.2f), new Vector3(1.5f, waterTop, 7.6f), new Vector3(-1.2f, waterTop, 9.4f), new Vector3(3.6f, waterTop, 5.4f) };
            for (int i = 0; i < floatSpots.Length; i++)
            {
                LobbyBobber(root, floatSpots[i], 0.2f + i * 0.22f, 2f + i * 0.35f, bobber =>
                {
                    bobber.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
                    var mat = Lit(stagePrefix + "LobbyFloat" + i, floatColors[i], 0.6f);
                    MeshObject(floatMesh, "Ring", bobber, Vector3.zero, Vector3.one, mat);
                });
            }
            LobbyBobber(root, new Vector3(3.5f, waterTop + 0.05f, 8.6f), 0.55f, 3.3f, bobber => BuildLobbyRaft(bobber, m));
            if (props.beachBall != null)
            {
                LobbyBobber(root, new Vector3(-3.4f, waterTop + 0.35f, 8.2f), 0.15f, 2.4f, bobber =>
                    SpawnFitted(props.beachBall, bobber, bobber.position, 0.85f, false, Fit.Center, false, random));
            }

            Debug.Log("[Fart Raft] Lobby: " + (palmsPlaced > 0 ? palmsPlaced + " tropical palms" : "primitive trees")
                + ", pier " + (Named(tropical, "pier").Length > 0 ? "yes" : "no")
                + ", tent " + (Named(tropical, "tent").Length > 0 ? "yes" : "no") + ".");

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

        static Material LobbyPoolMaterial(Mats m)
        {
            var pool = WaterMaterial(stagePrefix + "LobbyPool", m.slideWater.mainTexture, stage.waterLight);
            var texture = m.slideWater.mainTexture;
            pool.mainTexture = texture;
            if (pool.HasProperty("_BaseMap")) pool.SetTexture("_BaseMap", texture);
            pool.mainTextureScale = new Vector2(3.5f, 2.4f);
            EditorUtility.SetDirty(pool);
            return pool;
        }

        static bool PlaceLobbyProp(GameObject[] set, Transform root, Vector3 localPos, float size, bool byHeight, float yaw, System.Random random)
        {
            return TrySpawnYaw(set, root, root.TransformPoint(localPos), size, byHeight, yaw, random);
        }

        static void LobbyBobber(Transform root, Vector3 localPos, float phase, float period, System.Action<Transform> build)
        {
            var go = new GameObject("Bobber");
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPos;
            var bob = go.AddComponent<Bob>();
            bob.height = 0.07f;
            bob.period = period;
            bob.phase = phase;
            build(go.transform);
        }

        static void BuildLobbyRaft(Transform parent, Mats m)
        {
            parent.localRotation = Quaternion.Euler(0f, 28f, 0f);
            var ring = TorusMesh("LobbyRaft", 0.82f, 0.15f, 20, 8);
            MeshObject(ring, "Tube", parent, new Vector3(0f, 0.05f, 0f), Vector3.one, m.raftTube);
            MeshObject(CylinderMesh("LobbyRaftFloor", 0.7f, 0.05f, 14, false), "Floor", parent, new Vector3(0f, 0.08f, 0f), Vector3.one, m.raftFloor);
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
                if (real)
                {
                    var map = UIObject("Map", faceRt);
                    Place(map, center, new Vector2(0f, 8f), new Vector2(cardW - 28f, 78f));
                    var raw = map.gameObject.AddComponent<RawImage>();
                    raw.texture = StageMap(Stages[k]);
                    raw.color = new Color(1f, 1f, 1f, 0.95f);
                    raw.raycastTarget = false;
                }

                if (!real) continue;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = faceRt.GetComponent<Image>();
                manager.stageCards[k] = button;
                manager.stageHighlights[k] = highlight.gameObject;
                highlight.gameObject.SetActive(k == 0);
            }

            manager.playButton = MakeButton(lobby, "Play", "PLAY!", new Vector2(1f, 0f), new Vector2(-210f, 100f), new Vector2(340f, 140f), new Color(0.35f, 0.8f, 0.25f), 64);
            string[] modeLabels = { "RACE", "TEAM", "RUMBLE" };
            manager.modeButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var button = MakeButton(lobby, "Mode" + i, modeLabels[i], new Vector2(1f, 0f),
                    new Vector2(-210f, 250f + i * 86f), new Vector2(340f, 74f), new Color(0.22f, 0.24f, 0.34f, 0.96f), 36);
                var label = button.GetComponentInChildren<Text>();
                if (label != null) FitText(label, 22, 36);
                manager.modeButtons[i] = button;
            }

            // SOLO / 2 / 3 / 4, on the same row as CHOOSE STAGE.
            string[] countLabels = { "SOLO", "2", "3", "4" };
            const float countW = 156f, countH = 78f, countGap = 12f;
            float countX = -162f;
            manager.countButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var button = MakeButton(lobby, "Count" + (i + 1), countLabels[i], center,
                    new Vector2(countX + i * (countW + countGap), -230f), new Vector2(countW, countH),
                    new Color(0.22f, 0.24f, 0.34f, 0.96f), i == 0 ? 34 : 42);
                var label = button.GetComponentInChildren<Text>();
                if (label != null) FitText(label, 22, i == 0 ? 34 : 42);
                manager.countButtons[i] = button;
            }
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
            Label(box, "ModeHint", "EASY = the raft goes by itself. HARD = like the real game. RACE is you alone. TEAM races a yellow raft. RUMBLE knocks it off an island.", 24, center, new Vector2(0f, -210f), new Vector2(760f, 70f));
            manager.settingsCloseButton = MakeButton(box, "Close", "CLOSE", center, new Vector2(0f, -300f), new Vector2(360f, 90f), PlayerColors[2], 42);
            panel.gameObject.SetActive(false);
        }

        static AudioClip StageMusic(int index)
        {
            string name = "music_stage" + (index + 1);
            if (AssetImporter.GetAtPath(AudioFolder + "/Music/" + name + ".ogg") == null)
                name = "music_stage" + ((index % 3) + 1);
            return LoadMusic(name);
        }

        static Texture2D StageMap(Stage st)
        {
            const int size = 96;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var w in st.waypoints)
            {
                minX = Mathf.Min(minX, w.position.x);
                maxX = Mathf.Max(maxX, w.position.x);
                minZ = Mathf.Min(minZ, w.position.z);
                maxZ = Mathf.Max(maxZ, w.position.z);
            }
            float span = Mathf.Max(maxX - minX, maxZ - minZ, 1f);
            var pixels = new Color[size * size];
            Color paper = new Color(0.12f, 0.14f, 0.18f, 0.35f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = paper;

            Vector2 Plot(Vector3 p)
            {
                float u = (p.x - (minX + maxX) * 0.5f) / span + 0.5f;
                float v = (p.z - (minZ + maxZ) * 0.5f) / span + 0.5f;
                return new Vector2(u * (size - 8) + 4f, v * (size - 8) + 4f);
            }
            void Dot(int x, int y, Color color)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) return;
                pixels[y * size + x] = color;
            }
            void Stroke(Vector2 a, Vector2 b, Color color, int radius)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                    for (int dy = -radius; dy <= radius; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                            if (dx * dx + dy * dy <= radius * radius) Dot(Mathf.RoundToInt(p.x + dx), Mathf.RoundToInt(p.y + dy), color);
                }
            }

            for (int i = 1; i < st.waypoints.Length; i++)
                Stroke(Plot(st.waypoints[i - 1].position), Plot(st.waypoints[i].position), new Color(0.45f, 0.85f, 1f, 1f), 3);
            Stroke(Plot(st.waypoints[0].position), Plot(st.waypoints[0].position), new Color(0.4f, 0.95f, 0.4f, 1f), 5);
            var end = st.waypoints[st.waypoints.Length - 1].position;
            Stroke(Plot(end), Plot(end), new Color(1f, 0.35f, 0.4f, 1f), 5);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            string path = GeneratedFolder + "/" + st.name.Replace(" ", "") + "Map.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
