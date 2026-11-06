using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>A real-money product. Cosmetics and "Sans pub" only: nothing bought ever gives run power.</summary>
    public class ProductDef
    {
        public string id;
        public string name;
        public string description;
        /// <summary>Display price (a real store gives the localized price; mock uses this).</summary>
        public string price;
        /// <summary>Other products granted with this one (a pack can include "Sans pub").</summary>
        public string[] includes = Array.Empty<string>();
    }

    /// <summary>
    /// What an in-app purchase backend must provide. Plug Unity IAP / Google Play Billing / StoreKit by writing one
    /// class that implements this and assigning it in <see cref="Store.CreateProvider"/>. Callbacks: exactly once, main thread.
    /// </summary>
    public interface IStoreProvider
    {
        bool IsTest { get; }
        /// <summary>onDone(true) when paid (the Store then records the ownership).</summary>
        void Purchase(ProductDef product, Action<bool> onDone);
        /// <summary>Returns the product ids the account owns (non-consumables).</summary>
        void Restore(Action<List<string>> onDone);
    }

    public static class Store
    {
        public const string NoAdsId = "no_ads";
        public const string FounderId = "pack_founder";
        public const string NeonId = "pack_neon";
        public const string DemonId = "skin_demon";

        public static readonly List<ProductDef> Products = new()
        {
            new ProductDef
            {
                id = NoAdsId, name = "Sans pub", price = "1,99 €",
                description = "Plus aucune pub forcée avant les parties. Les pubs récompensées restent au choix. Titre « Mécène » offert.",
            },
            new ProductDef
            {
                id = FounderId, name = "Pack Fondateur", price = "4,99 €", includes = new[] { NoAdsId },
                description = "Skin Fondateur (doré + cape), titre « Fondateur » et Sans pub inclus. Merci de soutenir le jeu !",
            },
            new ProductDef
            {
                id = NeonId, name = "Pack Néon", price = "2,99 €",
                description = "Skin Néon (aura et antenne lumineuses) et titre « Électrique ».",
            },
            new ProductDef
            {
                id = DemonId, name = "Skin Démon", price = "1,99 €",
                description = "Cornes, peau rouge sombre et aura de braise.",
            },
        };

        static IStoreProvider provider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            provider = null;
            Busy = false;
        }

        public static IStoreProvider Provider
        {
            get => provider ??= CreateProvider();
            set => provider = value;
        }

        static IStoreProvider CreateProvider()
        {
            // Real store later, e.g.: return new UnityIapStoreProvider();
            return new MockStoreProvider();
        }

        public static bool IsTest => Provider.IsTest;
        public static bool Busy { get; private set; }

        public static event Action Changed;

        public static ProductDef Get(string id) => Products.Find(p => p.id == id);

        /// <summary>Owned directly, or through a pack that includes it.</summary>
        public static bool Owns(string id)
        {
            var list = Monetization.Data.purchases;
            if (list.Contains(id)) return true;
            foreach (var p in Products)
                if (list.Contains(p.id) && Array.IndexOf(p.includes, id) >= 0) return true;
            return false;
        }

        public static void Purchase(string id, Action<bool> onDone)
        {
            var product = Get(id);
            if (product == null || Busy)
            {
                onDone?.Invoke(false);
                return;
            }
            if (Owns(id))
            {
                onDone?.Invoke(true);
                return;
            }
            Busy = true;
            Provider.Purchase(product, ok =>
            {
                Busy = false;
                if (ok) Grant(id);
                onDone?.Invoke(ok);
            });
        }

        /// <summary>"Restaurer les achats": asks the store what the account owns. onDone gets how many products were restored.</summary>
        public static void Restore(Action<int> onDone)
        {
            if (Busy)
            {
                onDone?.Invoke(0);
                return;
            }
            Busy = true;
            Provider.Restore(ids =>
            {
                Busy = false;
                int n = 0;
                if (ids != null)
                    foreach (var id in ids)
                        if (Get(id) != null && !Monetization.Data.purchases.Contains(id))
                        {
                            Monetization.Data.purchases.Add(id);
                            n++;
                        }
                if (n > 0) MetaSave.Save();
                Changed?.Invoke();
                onDone?.Invoke(n);
            });
        }

        static void Grant(string id)
        {
            var list = Monetization.Data.purchases;
            if (!list.Contains(id)) list.Add(id);
            MetaSave.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Test store: a confirm popup "Achat (test) 1,99 €" then success. No real payment.</summary>
    public class MockStoreProvider : IStoreProvider
    {
        public bool IsTest => true;

        public void Purchase(ProductDef product, Action<bool> onDone)
        {
            var canvas = MonetizationUI.NewCanvas("Mock Store", MonetizationUI.StoreOrder);
            var bg = UIKit.Overlay("Store", canvas, 0.8f);
            var card = MetaUI.Card(bg, "Card", UIKit.Center, Vector2.zero, new Vector2(900, 760), UIKit.Card);
            UIAnim.SlideIn(card, new Vector2(0, -60), 0f, 0.25f, 0.85f);

            MetaUI.Text(card, "ACHAT (test)", 52, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -40), new Vector2(840, 70), FontStyle.Bold);
            MetaUI.Text(card, product.name, 60, Color.white, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -130), new Vector2(840, 80), FontStyle.Bold);
            MetaUI.Text(card, product.description, 32, UIKit.Muted, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -230), new Vector2(800, 170));
            MetaUI.Text(card, $"Achat (test) {product.price}", 50, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -420), new Vector2(840, 70), FontStyle.Bold);
            MetaUI.Text(card, "Boutique de test : aucun paiement réel.", 28, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Bottom, new Vector2(0, 190), new Vector2(840, 40));

            bool done = false;
            void Close(bool ok)
            {
                if (done) return;
                done = true;
                UnityEngine.Object.Destroy(canvas.gameObject);
                onDone?.Invoke(ok);
            }

            UIKit.Button(card, "Annuler", UIKit.Dark, Vector2.zero, new Vector2(40, 40), new Vector2(380, 130), () => Close(false), 44);
            var buy = UIKit.Button(card, "CONFIRMER", MetaUI.Green, new Vector2(1f, 0f), new Vector2(-40, 40), new Vector2(400, 130), () => Close(true), 46);
            UIAnim.Pulse((RectTransform)buy.transform, 3f, 0.03f);
        }

        public void Restore(Action<List<string>> onDone)
        {
            // Nothing to fetch: the mock's purchases already live in the local save.
            onDone?.Invoke(new List<string>(Monetization.Data.purchases));
        }
    }
}
