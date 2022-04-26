namespace Models.Medew
{
    public class Company
    {
        public int CompanyID { get; set; }
        public string Name { get; set; }
        
    }

    static class Extensions
    {
        public static string Modify(this string val)
        {
            return val;
        }
    }
}