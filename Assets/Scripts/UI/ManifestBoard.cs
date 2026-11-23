using ConductorChaos.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    internal sealed class ManifestBoard
    {
        private readonly GameObject root;
        private readonly Text[] fareLabels = new Text[2];
        private readonly Text[] fareMarks = new Text[2];
        private readonly Image[] fareStubs = new Image[2];
        private readonly RawImage[] pictures = new RawImage[3];
        private readonly Text[] captions = new Text[3];
        private readonly Text collectTitle;
        private static Vector2 V(float x, float y) => new(x, y);

        internal ManifestBoard(Transform parent)
        {
            var board = ChaosUI.Rect(parent, "Briefingbord", V(.023f, .022f), V(.977f, .71f), Vector2.zero, Vector2.zero);
            root = board.gameObject;
            var fare = Tile(board, 0, "Geldige kaartjes");
            for (int i = 0; i < fareLabels.Length; i++)
            {
                float left = .065f + i * .46f;
                var paper = ChaosUI.Panel(fare, "Kaartje", V(left, .29f), V(left + .41f, .69f), ChaosUI.Paper);
                var stub = ChaosUI.Panel(paper, "Kleurstrook", V(.48f, 0f), V(1f, 1f), ChaosUI.Gold);
                fareStubs[i] = stub.GetComponent<Image>();
                fareMarks[i] = Text(stub, "Letter", 21, ChaosUI.Ink, V(0f, 0f), V(1f, 1f));
                fareLabels[i] = Text(fare, "Kleur", 15, ChaosUI.Paper, V(left - .02f, .035f), V(left + .43f, .26f));
            }

            var atlas = Resources.Load<Texture2D>("ManifestPictures");
            var collect = Tile(board, 1, "Ophalen", out collectTitle);
            var leave = Tile(board, 2, "Laten staan");
            var refuse = Tile(board, 3, "Niet laten instappen");
            var tiles = new Transform[] { collect, leave, refuse };
            for (int i = 0; i < tiles.Length; i++)
            {
                var frame = ChaosUI.Rect(tiles[i], "Afbeelding", V(.08f, .22f), V(.92f, .75f), Vector2.zero, Vector2.zero);
                var image = ChaosUI.Rect(frame, "Illustratie", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 1f;
                pictures[i] = image.gameObject.AddComponent<RawImage>();
                pictures[i].texture = atlas;
                pictures[i].raycastTarget = false;
                captions[i] = Text(tiles[i], "Naam", 17, ChaosUI.Paper, V(0f, .025f), V(1f, .21f));
            }
            Hide();
        }

        internal void Show(ServiceManifest manifest, string pickupStation)
        {
            root.SetActive(true);
            for (int i = 0; i < fareMarks.Length; i++)
            {
                int code = i == 0 ? manifest.FirstFare : manifest.SecondFare;
                fareStubs[i].color = ServiceManifest.FareColor(code);
                fareMarks[i].text = ServiceManifest.FareMark(code);
                fareMarks[i].color = code == 3 ? ChaosUI.Ink : Color.white;
                fareLabels[i].text = ServiceManifest.FareName(code);
            }
            collectTitle.text = "Verplicht · " + pickupStation;
            Picture(0, manifest.RequiredFreight, false, ServiceManifest.FreightNames[manifest.RequiredFreight]);
            Picture(1, manifest.ExcludedFreight, false, ServiceManifest.FreightNames[manifest.ExcludedFreight]);
            Picture(2, manifest.ExcludedTraveller, true, ServiceManifest.TravellerNames[manifest.ExcludedTraveller]);
        }

        internal void Hide() => root.SetActive(false);

        private void Picture(int tile, int column, bool portrait, string caption)
        {
            pictures[tile].uvRect = new Rect(column * .25f, portrait ? .5f : 0f, .25f, .5f);
            captions[tile].text = caption;
        }

        private static RectTransform Tile(Transform parent, int i, string header) => Tile(parent, i, header, out _);

        private static RectTransform Tile(Transform parent, int i, string header, out Text title)
        {
            var tile = ChaosUI.Panel(parent, header, V(i * .254f, 0f), V(i * .254f + .238f, 1f), ChaosUI.Dark);
            title = Text(tile, header, 16, i >= 2 ? new Color32(244, 177, 157, 255) : ChaosUI.GoldLight,
                V(0f, .76f), V(1f, .97f));
            title.text = header;
            return tile;
        }

        private static Text Text(Transform parent, string name, int size, Color ink, Vector2 min, Vector2 max)
        {
            var caption = ChaosUI.Label(parent, name, "", size, ink, TextAnchor.MiddleCenter, FontStyle.Bold, min, max);
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 10;
            caption.resizeTextMaxSize = size;
            return caption;
        }
    }
}
