using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess
{
	public class BoundingBox
	{
		[Key]
		public int Id { get; set; }
		public int EventId { get; set; }
		[ForeignKey(nameof(EventId))]
		public Event? Event { get; set; }
		public float X1 { get; set; }
		public float Y1 { get; set; }
		public float Width { get; set; }
		public float Height { get; set; }
		public string? Label { get; set; }
		public float Probability { get; set; }
	}
}
