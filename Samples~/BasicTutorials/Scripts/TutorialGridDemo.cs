using System.Collections.Generic;
using TMPro;
using TutorialKit;
using UnityEngine;
using UnityEngine.UI;

namespace TutorialKitDemo
{
    /// <summary>
    /// Demonstrates DYNAMIC targets. Builds a grid of tiles at runtime, uses game logic to pick two
    /// "matching" tiles, and registers them as dynamic targets whose element is found by a resolver
    /// each frame — so a tutorial can highlight them (multi-hole vignette) and show a merge hint
    /// (pointer) even though the tiles didn't exist at author time and keep moving.
    /// </summary>
    public sealed class TutorialGridDemo : MonoBehaviour
    {
        [SerializeField] private TutorialGraph gridTutorial;
        [SerializeField] private int columns = 5;
        [SerializeField] private int rows = 4;

        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.30f, 0.35f), new Color(0.30f, 0.65f, 0.90f),
            new Color(0.40f, 0.80f, 0.45f), new Color(0.95f, 0.75f, 0.30f),
            new Color(0.65f, 0.45f, 0.85f), new Color(0.35f, 0.80f, 0.80f),
        };

        private readonly Dictionary<string, RectTransform> _cells = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, int> _cellColor = new Dictionary<string, int>();
        private RectTransform _grid;
        private string _keyA, _keyB;

        private void Start()
        {
            var dir = TutorialDirector.EnsureExists();
            BuildGrid();
            PickTwoMatching();

            // The target elements are found by game logic each time (dynamic) — not fixed references.
            TutorialTargets.RegisterDynamic("grid_a", () => FindCell(_keyA));
            TutorialTargets.RegisterDynamic("grid_b", () => FindCell(_keyB));

            if (gridTutorial != null) dir.Play(gridTutorial, force: true);
        }

        private Transform FindCell(string key) =>
            key != null && _cells.TryGetValue(key, out var rt) && rt != null ? rt : null;

        // Game logic: find two tiles that "match" (same colour here).
        private void PickTwoMatching()
        {
            var seen = new Dictionary<int, string>();
            foreach (var kv in _cellColor)
            {
                if (seen.TryGetValue(kv.Value, out var first)) { _keyA = first; _keyB = kv.Key; return; }
                seen[kv.Value] = kv.Key;
            }
            // Fallback: first two cells.
            var e = _cells.Keys.GetEnumerator();
            if (e.MoveNext()) _keyA = e.Current;
            if (e.MoveNext()) _keyB = e.Current;
        }

        private void BuildGrid()
        {
            var canvasGo = new GameObject("GridCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var title = new GameObject("Title", typeof(RectTransform));
            var trt = (RectTransform)title.transform;
            trt.SetParent(canvasGo.transform, false);
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -40);
            trt.sizeDelta = new Vector2(800, 80);
            var tt = title.AddComponent<TextMeshProUGUI>();
            tt.text = "Match-3 Grid"; tt.fontSize = 44; tt.fontStyle = FontStyles.Bold;
            tt.alignment = TextAlignmentOptions.Center; tt.color = Color.white; tt.raycastTarget = false;

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            _grid = (RectTransform)gridGo.transform;
            _grid.SetParent(canvasGo.transform, false);
            _grid.anchorMin = _grid.anchorMax = _grid.pivot = new Vector2(0.5f, 0.5f);
            var glg = gridGo.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(110, 110);
            glg.spacing = new Vector2(14, 14);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = columns;
            _grid.sizeDelta = new Vector2(columns * 124f, rows * 124f);

            int n = 1;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    string key = r + "_" + c;
                    int colorIdx = (r * columns + c) % Palette.Length;
                    var cellGo = new GameObject("Cell_" + key, typeof(RectTransform), typeof(Image));
                    var crt = (RectTransform)cellGo.transform;
                    crt.SetParent(_grid, false);
                    var img = cellGo.GetComponent<Image>();
                    img.color = Palette[colorIdx];
                    _cells[key] = crt;
                    _cellColor[key] = colorIdx;

                    var lblGo = new GameObject("N", typeof(RectTransform));
                    var lrt = (RectTransform)lblGo.transform;
                    lrt.SetParent(crt, false);
                    lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                    var lbl = lblGo.AddComponent<TextMeshProUGUI>();
                    lbl.text = (n++).ToString(); lbl.fontSize = 28; lbl.alignment = TextAlignmentOptions.Center;
                    lbl.color = new Color(1, 1, 1, 0.85f); lbl.raycastTarget = false;
                }
        }

        // Gently drift the grid so the dynamic targets visibly move — the highlights follow them.
        private void Update()
        {
            if (_grid == null) return;
            float x = Mathf.Sin(Time.time * 0.8f) * 26f;
            float y = Mathf.Cos(Time.time * 0.6f) * 18f;
            _grid.anchoredPosition = new Vector2(x, y);
        }
    }
}
