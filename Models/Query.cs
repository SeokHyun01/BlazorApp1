using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
	public class Query
	{
		public string? Date { get; set; }
		public string? UserId { get; set; }
		public string? Image { get; set; }
		public IEnumerable<WeakBoundingBox>? BoundingBoxes { get; set; }
	}
}
