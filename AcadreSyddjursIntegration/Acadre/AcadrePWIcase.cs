using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcadrePWI.Acadre
{
    public class AcadrePWIcase
    {
        public int Id { get; set; }
        public int SequenceNumber { get; set; }
        public int Year { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        //public Classification
        public CaseType CaseType { get; set; }
        //public //CaseSubTybe
        public ResponsibleUnit ResponsibleUnit { get; set; }
        public PublicAccessLevel PublicAccessLevel { get; set; }
        public string AccessCode { get; set; }
        public int ResponsibleUserId { get; set; }
        public string ResponsibleUserName { get; set; }
        public Status Status { get; set; }
        public DateTime CaseDate { get; set; }
        public string Content { get; set; }
        public CustomField[] CustomFields { get; set; }
    }
    public struct CaseType { public bool IsChildYouth; public bool IsCitizen; public string Literal; public string Description; };
    public struct ResponsibleUnit { public int Id; public string Name; };
    public struct PublicAccessLevel { public int Id; public string Name; };
    public struct Status { public string Literal; public string Description; public bool IsClosed; public bool IsPaused; };
    public struct CustomField { public string Name; public string Value; };
}
