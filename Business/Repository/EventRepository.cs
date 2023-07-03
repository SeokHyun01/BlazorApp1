using AutoMapper;
using Business.Repository.IRepository;
using DataAccess;
using DataAccess.Data;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Repository
{
	public class EventRepository : IEventRepository
	{
		private readonly AppDbContext _db;
		private readonly IMapper _mapper;


		public EventRepository(AppDbContext db, IMapper mapper)
		{
			_db = db;
			_mapper = mapper;
		}
		
		public async ValueTask<EventDTO> Create(EventDTO objDTO)
		{
			try
			{
				var obj = _mapper.Map<EventDTO, Event>(objDTO);
				var createdObj = _db.Events.Add(obj);
				await _db.SaveChangesAsync();

				return _mapper.Map<Event, EventDTO>(createdObj.Entity);
			}

			catch (Exception ex)
			{
				throw new Exception($"{ex.GetType()}: {ex.Message}");
			}
		}

		public async ValueTask<int> Delete(int id)
		{
			throw new NotImplementedException();
		}

        public async ValueTask<IEnumerable<EventDTO>> GetAll(string id)
        {
			if (string.IsNullOrEmpty(id))
			{
				return Enumerable.Empty<EventDTO>();
			}
            return _mapper.Map<IEnumerable<Event>, IEnumerable<EventDTO>>(_db.Events.Where(x => x.UserId == id));
        }
    }
}
