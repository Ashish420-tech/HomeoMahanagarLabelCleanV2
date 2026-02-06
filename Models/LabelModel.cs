using System;

namespace HomeoMahanagarLabelCleanV2.Models
{
    public class LabelModel
    {
        public string MedicineName { get; set; }
        public string LatinName { get; set; }
        public string BatchNo { get; set; }
        public DateTime MfgDate { get; set; }
        public DateTime ExpDate { get; set; }
    }
}
