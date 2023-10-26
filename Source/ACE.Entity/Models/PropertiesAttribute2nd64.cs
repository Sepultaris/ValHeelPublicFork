public class PropertiesAttribute2nd64
{
    public uint InitLevel { get; set; }
    public uint LevelFromCP { get; set; }
    public uint CPSpent { get; set; }
    public uint CurrentLevel { get; set; }

    public PropertiesAttribute2nd64 Clone()
    {
        var result = new PropertiesAttribute2nd64
        {
            InitLevel = InitLevel,
            LevelFromCP = LevelFromCP,
            CPSpent = CPSpent,
            CurrentLevel = CurrentLevel,
        };

        return result;
    }
}
