using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETHBot.DataLayer.Data.ETH.Food
{
    public class Alergy
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AlergyId { get; set; }
        public string Name { get; set; }
        public string NameDE { get; set; }
        public string ImagePath { get; set; }
    }
}
