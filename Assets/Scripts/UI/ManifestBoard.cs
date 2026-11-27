using ConductorChaos.Playground;
using ConductorChaos.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    internal sealed class ManifestBoard
    {
        private readonly GameObject root;
        private readonly Transform[] fareTickets = new Transform[2];
        private readonly TextMesh[][] fareMarks = new TextMesh[2][];
        private readonly RawImage[] pictures = new RawImage[3];
        private readonly Text[] captions = new Text[3];
        private readonly Text collectTitle;
        private static Vector2 V(float x, float y) => new(x, y);

        internal ManifestBoard(Transform parent)
        {
            var board = ChaosUI.Rect(parent, "Briefingbord", V(.028f, .04f), V(.972f, .79f), Vector2.zero, Vector2.zero);
            root = board.gameObject;
            var fare = Tile(board, 0, "Geldige kaartjes");
            for (int i = 0; i < fareTickets.Length; i++)
            {
                var anchor = V(.5f, .545f - i * .31f);
                ChaosUI.Panel(fare, "Kaartmat", V(.085f, anchor.y - .145f), V(.915f, anchor.y + .145f), ChaosUI.Hex("#BEB6A5"));
                var holder = ChaosUI.Rect(fare, "Voorbeeldkaartje", anchor, anchor, Vector2.zero, Vector2.zero);
                holder.localPosition += Vector3.back * 4.3f;
                fareTickets[i] = PassengerTicket.CreateDisplayTicket(holder);
                if (fareTickets[i]) fareTickets[i].localScale = Vector3.one * 920f;
            }

            var atlas = Resources.Load<Texture2D>("ManifestPictures");
            var collect = Tile(board, 1, "Ophalen", out collectTitle);
            var leave = Tile(board, 2, "Laten staan");
            var refuse = Tile(board, 3, "Niet laten instappen");
            var tiles = new Transform[] { collect, leave, refuse };
            for (int i = 0; i < tiles.Length; i++)
            {
                var frame = ChaosUI.Rect(tiles[i], "Afbeelding", V(.08f, .22f), V(.92f, .73f), Vector2.zero, Vector2.zero);
                var image = ChaosUI.Rect(frame, "Illustratie", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 1f;
                pictures[i] = image.gameObject.AddComponent<RawImage>();
                pictures[i].texture = atlas;
                pictures[i].raycastTarget = false;
                captions[i] = Text(tiles[i], "Naam", 23, ChaosUI.Ink, V(0f, .025f), V(1f, .21f));
            }
            Hide();
        }

        internal void Show(ServiceManifest manifest, string pickupStation)
        {
            root.SetActive(true);
            PassengerTicket.PresentFare(fareTickets[0], manifest.FirstFare, ref fareMarks[0]);
            PassengerTicket.PresentFare(fareTickets[1], manifest.SecondFare, ref fareMarks[1]);
            collectTitle.text = "Ophalen · " + pickupStation;
            Picture(0, manifest.RequiredFreight, false, ServiceManifest.FreightNames[manifest.RequiredFreight]);
            Picture(1, manifest.ExcludedFreight, false, ServiceManifest.FreightNames[manifest.ExcludedFreight]);
            int traveller = manifest.ExcludedTraveller;     // de muzikant staat in het vrije vak rechtsonder
            Picture(2, traveller < 4 ? traveller : 3, traveller < 4, ServiceManifest.TravellerNames[traveller]);
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
            var tile = ChaosUI.Panel(parent, header, V(i * .254f, 0f), V(i * .254f + .238f, 1f), ChaosUI.Paper);
            title = Text(tile, header, 23, i >= 2 ? ChaosUI.TrainRedDark : ChaosUI.Ink,
                V(0f, .75f), V(1f, .97f));
            title.text = header;
            return tile;
        }

        private static Text Text(Transform parent, string name, int size, Color ink, Vector2 min, Vector2 max)
        {
            var caption = ChaosUI.Label(parent, name, "", size, ink, TextAnchor.MiddleCenter, FontStyle.Bold, min, max);
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 15;
            caption.raycastTarget = false;
            caption.resizeTextMaxSize = size;
            return caption;
        }
    }
}
