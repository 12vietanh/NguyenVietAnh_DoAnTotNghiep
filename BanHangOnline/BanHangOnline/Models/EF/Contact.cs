﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BanHangOnline.Models.EF
{
    [Table("tb_Contact")]
    public class Contact : CommonAstract
    {
        [Key]
        [DatabaseGeneratedAttribute(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên không được để trống")]
        [StringLength(250, ErrorMessage = "Không được vượt quá 250 ký tự")]
        public string Name { get; set; }

        [StringLength(250, ErrorMessage = "Không được vượt quá 250 ký tự")]
        public string Website { get; set; }

        [StringLength(250, ErrorMessage = "Không được vượt quá 250 ký tự")]
        public string Email { get; set; }

        [StringLength(4000, ErrorMessage = "Không được vượt quá 4000 ký tự")]
        public string Message { get; set; }
        public bool IsRead { get; set; }
    }
}