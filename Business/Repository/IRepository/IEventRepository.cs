using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Repository.IRepository
{
	public interface IEventRepository
	{
		ValueTask<EventDTO> Create(EventDTO objDTO);
		ValueTask<int> Delete(int id);
		ValueTask<IEnumerable<EventDTO>> GetAll(string id);
    }
}
