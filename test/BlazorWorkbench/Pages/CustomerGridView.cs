using Enums.Status;

namespace Models.Account
{
    public class CustomerGridView
    {
        public Customer CustomerStatus;
        public string CustomerName;
        public string CompanyName;
        public int CompanyID;
        public object? Period1Toegekend;
        public object? Period1Realisatie;
        public object? Period2Toegekend;
        public object? Period2Realisatie;
        public object? Period3Toegekend;
        public object? Period3Realisatie;
        public int CustomerID { get; set; }
    }
}

namespace Models.Medew 
{
    
}

namespace Enums.Status
{
    public enum Customer
    {
        Active,
        Deactivated
    }
    
}