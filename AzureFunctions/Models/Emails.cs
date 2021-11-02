using System;
using System.Collections.Generic;
using System.Text;

namespace AzureFunctions.Models
{
    public class Emails
    {
        public long Id { get; set; }
        public string EmailAddress { get; set; }
        public string FullName { get; set; }
    }
}
