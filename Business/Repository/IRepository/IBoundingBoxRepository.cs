using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Repository.IRepository
{
	public interface IBoundingBoxRepository
	{
		ValueTask<int> Create(IEnumerable<BoundingBoxDTO> objDTOs);
		ValueTask<int> Delete(int id);
		ValueTask<IEnumerable<BoundingBoxDTO>> GetAll(int id);
    }
}
