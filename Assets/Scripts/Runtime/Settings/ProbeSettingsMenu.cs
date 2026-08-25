using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// The on-screen menu an experimenter steers a session with.
    /// </summary>
    /// <remarks>
    /// <para>Lifted out of <see cref="BlindModeOverlay"/>, which was carrying
    /// the blind condition, the keyboard shortcuts, the status text, the debug
    /// minimap and this menu at once. The menu is the part that grows every
    /// time a new thing becomes adjustable, so it is the part that had to
    /// leave before control modes and sound sets are added to it.</para>
    ///
    /// <para>Rows are declared rather than drawn one after another. The panel
    /// used to size itself with a hand-counted multiple of the row height,
    /// which meant every new row was two edits and a guess, and the guess was
    /// already four pixels short. Measuring the rows it actually holds costs
    /// nothing and cannot fall behind them.</para>
    ///
    /// <para>This is for whoever is running the session, not for the person in
    /// it. It draws over the blind fill on purpose: an experimenter has to move
    /// between layouts to run a trial, and a build with no reachable menu is a
    /// black screen nobody can steer. The one control that would end the blind
    /// condition is compiled out of a shipped build instead.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ProbeSettingsMenu : MonoBehaviour
    {
        private sealed class MenuRow
        {
            public Func<string> Text;

            /// <summary>Null for a label, which is drawn but cannot be hit.</summary>
            public Action Activate;

            /// <summary>Null when the row is always available.</summary>
            public Func<bool> Interactable;

            /// <summary>Pairs with the next half row to share one line.</summary>
            public bool Half;

            public Rect Rect;
        }

        private readonly List<MenuRow> rows = new List<MenuRow>();
        private BlindModeOverlay overlay;
        private ProbeHeading heading;
        private FeedbackCoordinator coordinator;
        private GUIStyle buttonStyle;
        private GUIStyle panelStyle;
        private bool open;
        private Rect gearRect;

        public void Configure(BlindModeOverlay blindOverlay, ProbeHeading probeHeading,
            FeedbackCoordinator feedback)
        {
            overlay = blindOverlay;
            heading = probeHeading;
            coordinator = feedback;
            BuildRows();
        }

        private void BuildRows()
        {
            rows.Clear();

            // Only a build that already carries the debug map offers a way out
            // of the blind condition, and the three map controls under it steer
            // a map a shipped build never draws. A participant holding a
            // release build can reach everything else here and not these.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Add(() => overlay.IsBlind ? "VIEW DEBUG MAP" : "RETURN TO BLIND MODE",
                () => overlay.SetBlind(!overlay.IsBlind));
            AddHalf(() => overlay.ShowMap ? "HIDE MAP" : "SHOW MAP",
                () => overlay.ShowMap = !overlay.ShowMap);
            AddHalf(() => overlay.LocalMapView ? "FULL 256m" : "LOCAL 40m",
                () => overlay.LocalMapView = !overlay.LocalMapView);
            Add(() => overlay.SourceClassView
                    ? "SHOW PEDESTRIAN SEMANTICS"
                    : "SHOW SOURCE CLASSES",
                () => overlay.SourceClassView = !overlay.SourceClassView);
#endif

            // Reachable in a shipped build, unlike everything above it. Which
            // frame a session walked in is a condition, not a diagnostic, and
            // an experimenter has to be able to set it on the handset that is
            // going to run the trial.
            Add(DescribeMovementFrame, ToggleMovementFrame);

            AddLabel(DescribeScene);
            AddHalf(() => "PREVIOUS SCENE", () => LoadSceneOffset(-1), CanLoadOffset(-1));
            AddHalf(() => "NEXT SCENE", () => LoadSceneOffset(1), CanLoadOffset(1));
        }

        private string DescribeMovementFrame() =>
            heading != null && heading.MovementFrame == ProbeMovementFrame.World
                ? "WALK: COMPASS (W = NORTH)"
                : "WALK: BODY (W = FACING)";

        private void ToggleMovementFrame()
        {
            if (heading == null)
                return;

            heading.MovementFrame = heading.MovementFrame == ProbeMovementFrame.World
                ? ProbeMovementFrame.Body
                : ProbeMovementFrame.World;

            // Into the session file, because a walked path means a different
            // thing under each frame and a file that does not say which one
            // cannot be read back correctly.
            coordinator?.SignalBus?.LogConfigurationChange("movement_frame",
                $"frame={heading.MovementFrame}");
        }

        private void Add(Func<string> text, Action activate,
            Func<bool> interactable = null) =>
            rows.Add(new MenuRow
            {
                Text = text,
                Activate = activate,
                Interactable = interactable
            });

        private void AddHalf(Func<string> text, Action activate,
            Func<bool> interactable = null) =>
            rows.Add(new MenuRow
            {
                Text = text,
                Activate = activate,
                Interactable = interactable,
                Half = true
            });

        private void AddLabel(Func<string> text) =>
            rows.Add(new MenuRow { Text = text });

        private void OnGUI()
        {
            // Depth rather than component order. Unity does not promise which
            // component's OnGUI runs first, and losing that race would put the
            // blind fill on top of the menu and leave the session unsteerable.
            GUI.depth = -1;

            if (overlay == null)
                return;

            float scale = Mathf.Clamp(Screen.dpi > 1f ? Screen.dpi / 320f : 1f, 1f, 1.6f);
            EnsureStyles(scale);

            float buttonSize = 58f * scale;
            float margin = 14f * scale;
            gearRect = new Rect(Screen.width - buttonSize - margin, margin,
                buttonSize, buttonSize);
            if (GUI.Button(gearRect, "⚙", buttonStyle))
                open = !open;

            if (!open)
            {
                ClearRowRects();
                return;
            }

            float width = Mathf.Min(390f * scale, Screen.width - margin * 2f);
            float panelX = Screen.width - width - margin;
            float panelY = gearRect.yMax + 8f * scale;
            float inset = 12f * scale;
            float innerWidth = width - inset * 2f;

            float panelHeight = LayOutRows(panelX + inset, panelY, innerWidth, scale);
            GUI.Box(new Rect(panelX, panelY, width, panelHeight), "TESTBED SETTINGS",
                panelStyle);
            DrawRows();
        }

        /// <summary>
        /// Places every row and answers how tall the panel has to be to hold
        /// them. The panel's own position does not depend on its height, so
        /// laying the rows out first and sizing the box afterwards keeps the
        /// two from ever disagreeing.
        /// </summary>
        private float LayOutRows(float x, float panelY, float innerWidth, float scale)
        {
            float headerHeight = 36f * scale;
            float rowHeight = 50f * scale;
            float labelHeight = rowHeight * 0.58f;
            float gap = 8f * scale;
            float inset = 12f * scale;
            float half = (innerWidth - gap) * 0.5f;

            float y = panelY + headerHeight;
            bool trailingGap = false;

            for (int i = 0; i < rows.Count; i++)
            {
                MenuRow row = rows[i];
                if (row.Activate == null)
                {
                    row.Rect = new Rect(x, y, innerWidth, labelHeight);
                    y += labelHeight;
                    trailingGap = false;
                    continue;
                }

                if (row.Half && i + 1 < rows.Count && rows[i + 1].Half &&
                    rows[i + 1].Activate != null)
                {
                    row.Rect = new Rect(x, y, half, rowHeight);
                    rows[i + 1].Rect = new Rect(x + half + gap, y, half, rowHeight);
                    i++;
                }
                else
                {
                    row.Rect = new Rect(x, y, row.Half ? half : innerWidth, rowHeight);
                }

                y += rowHeight + gap;
                trailingGap = true;
            }

            if (trailingGap)
                y -= gap;
            return y + inset - panelY;
        }

        private void DrawRows()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                MenuRow row = rows[i];
                string text = row.Text();
                if (row.Activate == null)
                {
                    GUI.Label(row.Rect, text, panelStyle);
                    continue;
                }

                GUI.enabled = row.Interactable == null || row.Interactable();
                if (GUI.Button(row.Rect, text))
                    row.Activate();
                GUI.enabled = true;
            }
        }

        private void EnsureStyles(float scale)
        {
            if (buttonStyle != null)
                return;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(27f * scale),
                alignment = TextAnchor.MiddleCenter
            };
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.RoundToInt(17f * scale),
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
        }

        private void ClearRowRects()
        {
            for (int i = 0; i < rows.Count; i++)
                rows[i].Rect = Rect.zero;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HandleTouch();
#endif
        }

        private void HandleTouch()
        {
#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return;

            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (!touches[i].press.wasPressedThisFrame)
                    continue;
                Vector2 screen = touches[i].position.ReadValue();
                HandleTap(new Vector2(screen.x, Screen.height - screen.y));
                break;
            }
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;
                HandleTap(new Vector2(touch.position.x,
                    Screen.height - touch.position.y));
                break;
            }
#endif
        }

        private void HandleTap(Vector2 guiPosition)
        {
            if (gearRect.Contains(guiPosition))
            {
                open = !open;
                return;
            }
            if (!open)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                MenuRow row = rows[i];
                if (row.Activate == null || !row.Rect.Contains(guiPosition))
                    continue;
                if (row.Interactable == null || row.Interactable())
                    row.Activate();
                return;
            }
        }

        private static string DescribeScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            int count = SceneManager.sceneCountInBuildSettings;
            return $"SCENE {scene.buildIndex + 1}/{Mathf.Max(1, count)}  {scene.name}";
        }

        private static Func<bool> CanLoadOffset(int offset) =>
            () => CanLoad(SceneManager.GetActiveScene().buildIndex, offset);

        private static void LoadSceneOffset(int offset)
        {
            int current = SceneManager.GetActiveScene().buildIndex;
            if (CanLoad(current, offset))
                SceneManager.LoadScene(current + offset);
        }

        // A scene opened in the editor but never added to the build settings
        // reports -1, and stepping forward from there would load scene zero
        // rather than doing nothing.
        private static bool CanLoad(int current, int offset)
        {
            if (current < 0)
                return false;
            int target = current + offset;
            return target >= 0 && target < SceneManager.sceneCountInBuildSettings;
        }
    }
}
