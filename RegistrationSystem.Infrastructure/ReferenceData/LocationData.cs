namespace RegistrationSystem.Infrastructure.ReferenceData;

/// <summary>
/// Static location data for supported countries and their states/provinces.
/// </summary>
public static class LocationData
{
    public static readonly IReadOnlyList<Country> Countries = new List<Country>
    {
        new("US", "United States", GetUSStates()),
        new("CA", "Canada", GetCanadianProvinces()),
        new("MX", "Mexico", GetMexicanStates())
    };

    public static Country? GetCountry(string code) =>
        Countries.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    public static Country? GetCountryByName(string name) =>
        Countries.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static List<StateProvince> GetUSStates() => new()
    {
        new("AL", "Alabama"),
        new("AK", "Alaska"),
        new("AZ", "Arizona"),
        new("AR", "Arkansas"),
        new("CA", "California"),
        new("CO", "Colorado"),
        new("CT", "Connecticut"),
        new("DE", "Delaware"),
        new("FL", "Florida"),
        new("GA", "Georgia"),
        new("HI", "Hawaii"),
        new("ID", "Idaho"),
        new("IL", "Illinois"),
        new("IN", "Indiana"),
        new("IA", "Iowa"),
        new("KS", "Kansas"),
        new("KY", "Kentucky"),
        new("LA", "Louisiana"),
        new("ME", "Maine"),
        new("MD", "Maryland"),
        new("MA", "Massachusetts"),
        new("MI", "Michigan"),
        new("MN", "Minnesota"),
        new("MS", "Mississippi"),
        new("MO", "Missouri"),
        new("MT", "Montana"),
        new("NE", "Nebraska"),
        new("NV", "Nevada"),
        new("NH", "New Hampshire"),
        new("NJ", "New Jersey"),
        new("NM", "New Mexico"),
        new("NY", "New York"),
        new("NC", "North Carolina"),
        new("ND", "North Dakota"),
        new("OH", "Ohio"),
        new("OK", "Oklahoma"),
        new("OR", "Oregon"),
        new("PA", "Pennsylvania"),
        new("RI", "Rhode Island"),
        new("SC", "South Carolina"),
        new("SD", "South Dakota"),
        new("TN", "Tennessee"),
        new("TX", "Texas"),
        new("UT", "Utah"),
        new("VT", "Vermont"),
        new("VA", "Virginia"),
        new("WA", "Washington"),
        new("WV", "West Virginia"),
        new("WI", "Wisconsin"),
        new("WY", "Wyoming"),
        new("DC", "District of Columbia"),
        new("PR", "Puerto Rico"),
        new("VI", "Virgin Islands"),
        new("GU", "Guam")
    };

    private static List<StateProvince> GetCanadianProvinces() => new()
    {
        new("AB", "Alberta"),
        new("BC", "British Columbia"),
        new("MB", "Manitoba"),
        new("NB", "New Brunswick"),
        new("NL", "Newfoundland and Labrador"),
        new("NS", "Nova Scotia"),
        new("NT", "Northwest Territories"),
        new("NU", "Nunavut"),
        new("ON", "Ontario"),
        new("PE", "Prince Edward Island"),
        new("QC", "Quebec"),
        new("SK", "Saskatchewan"),
        new("YT", "Yukon")
    };

    private static List<StateProvince> GetMexicanStates() => new()
    {
        new("AGU", "Aguascalientes"),
        new("BCN", "Baja California"),
        new("BCS", "Baja California Sur"),
        new("CAM", "Campeche"),
        new("CHP", "Chiapas"),
        new("CHH", "Chihuahua"),
        new("COA", "Coahuila"),
        new("COL", "Colima"),
        new("DUR", "Durango"),
        new("GUA", "Guanajuato"),
        new("GRO", "Guerrero"),
        new("HID", "Hidalgo"),
        new("JAL", "Jalisco"),
        new("MEX", "México"),
        new("MIC", "Michoacán"),
        new("MOR", "Morelos"),
        new("NAY", "Nayarit"),
        new("NLE", "Nuevo León"),
        new("OAX", "Oaxaca"),
        new("PUE", "Puebla"),
        new("QUE", "Querétaro"),
        new("ROO", "Quintana Roo"),
        new("SLP", "San Luis Potosí"),
        new("SIN", "Sinaloa"),
        new("SON", "Sonora"),
        new("TAB", "Tabasco"),
        new("TAM", "Tamaulipas"),
        new("TLA", "Tlaxcala"),
        new("VER", "Veracruz"),
        new("YUC", "Yucatán"),
        new("ZAC", "Zacatecas"),
        new("CMX", "Ciudad de México")
    };
}

public record Country(string Code, string Name, IReadOnlyList<StateProvince> StatesProvinces)
{
    public string StateLabel => Code switch
    {
        "US" => "State",
        "CA" => "Province",
        "MX" => "State",
        _ => "State/Province"
    };
}

public record StateProvince(string Code, string Name);