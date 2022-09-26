using ACE.Entity.Enum.Properties;

namespace ACE.Server.DuskfallMods
{
    enum RaiseTarget
    {
        //Match attributes with ACE.Entity.Enum.Properties.PropertyAttribute to work with casting
        Str = PropertyAttribute.Strength,
        End = PropertyAttribute.Endurance,
        Quick = PropertyAttribute.Quickness,
        Coord = PropertyAttribute.Coordination,
        Focus = PropertyAttribute.Focus,
        Self = PropertyAttribute.Self,
        //MaxHealth = PropertyAttribute2nd.MaxHealth,
        //MaxStamina = PropertyAttribute2nd.MaxStamina,
        //MaxMana = PropertyAttribute2nd.MaxMana,
        //Ratings
        World, Destruction, Invulnerability, Glory, Temperance, Vitality, Health, Stamina, Mana
    }
}
