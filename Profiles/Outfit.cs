using System.Collections.Generic;
using System.Xml.Serialization;
using Rage;
using Rage.Native;

namespace LSFD.Profiles
{
    [XmlRoot("Outfits")]
    public class OutfitList
    {
        [XmlElement("Outfit")]
        public List<Outfit> Outfits { get; set; } = new List<Outfit>();
    }

    /// <summary>
    /// A ped model with its clothing (components) and props. Works with EUP: dress up with the EUP menu on the freemode
    /// model, then save it as an outfit at the station locker room.
    /// </summary>
    public class Outfit
    {
        private const int ComponentCount = 12;
        private static readonly int[] PropSlots = { 0, 1, 2, 6, 7 };

        [XmlAttribute] public string Name { get; set; }
        /// <summary>Model name, e.g. mp_m_freemode_01 (EUP) or s_m_y_fireman_01.</summary>
        [XmlAttribute] public string Model { get; set; }

        /// <summary>Model hash, used instead of <see cref="Model"/> when set (saved outfits store it).</summary>
        [XmlAttribute] public uint ModelHash { get; set; }

        [XmlElement("Component")]
        public List<OutfitPart> Components { get; set; } = new List<OutfitPart>();

        [XmlElement("Prop")]
        public List<OutfitPart> Props { get; set; } = new List<OutfitPart>();

        /// <summary>What <paramref name="ped"/> is wearing right now.</summary>
        public static Outfit Capture(Ped ped, string name)
        {
            var outfit = new Outfit { Name = name, Model = ped.Model.Name, ModelHash = ped.Model.Hash };
            for (int id = 0; id < ComponentCount; id++)
            {
                outfit.Components.Add(new OutfitPart
                {
                    Id = id,
                    Drawable = NativeFunction.CallByName<int>("GET_PED_DRAWABLE_VARIATION", ped, id),
                    Texture = NativeFunction.CallByName<int>("GET_PED_TEXTURE_VARIATION", ped, id),
                });
            }
            foreach (int id in PropSlots)
            {
                outfit.Props.Add(new OutfitPart
                {
                    Id = id,
                    Drawable = NativeFunction.CallByName<int>("GET_PED_PROP_INDEX", ped, id),
                    Texture = NativeFunction.CallByName<int>("GET_PED_PROP_TEXTURE_INDEX", ped, id),
                });
            }
            return outfit;
        }

        /// <summary>Puts the outfit on the player, changing the model first when needed.</summary>
        public void ApplyToPlayer()
        {
            if (ModelHash != 0 || !string.IsNullOrEmpty(Model))
            {
                Model model = ModelHash != 0 ? new Model(ModelHash) : new Model(Model);
                if (model.IsValid && Game.LocalPlayer.Model.Hash != model.Hash)
                {
                    Game.LocalPlayer.Model = model;
                    Ped changed = Game.LocalPlayer.Character;
                    NativeFunction.CallByName<uint>("SET_PED_DEFAULT_COMPONENT_VARIATION", changed);
                    // The freemode models have no face until they get a head blend.
                    if (IsFreemode(model))
                        NativeFunction.CallByName<uint>("SET_PED_HEAD_BLEND_DATA", changed, 0, 0, 0, 0, 0, 0, 0f, 0f, 0f, false);
                }
            }

            Ped ped = Game.LocalPlayer.Character;
            foreach (OutfitPart part in Components)
                NativeFunction.CallByName<uint>("SET_PED_COMPONENT_VARIATION", ped, part.Id, part.Drawable, part.Texture, 0);
            foreach (OutfitPart part in Props)
            {
                if (part.Drawable < 0)
                    NativeFunction.CallByName<uint>("CLEAR_PED_PROP", ped, part.Id);
                else
                    NativeFunction.CallByName<uint>("SET_PED_PROP_INDEX", ped, part.Id, part.Drawable, part.Texture, true);
            }
        }

        private static bool IsFreemode(Model model)
        {
            return model.Hash == Game.GetHashKey("mp_m_freemode_01") || model.Hash == Game.GetHashKey("mp_f_freemode_01");
        }
    }

    public class OutfitPart
    {
        [XmlAttribute] public int Id { get; set; }
        [XmlAttribute] public int Drawable { get; set; }
        [XmlAttribute] public int Texture { get; set; }
    }
}
