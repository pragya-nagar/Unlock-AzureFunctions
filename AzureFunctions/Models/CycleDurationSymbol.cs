
namespace AzureFunctions.Models
{
    public class CycleDurationSymbol
    {
        public int Id
        {
            get;
            set;
        }

        public long CycleDurationId
        {
            get;
            set;
        }

        public string Symbol
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public bool IsActive
        {
            get;
            set;
        }
    }
}
