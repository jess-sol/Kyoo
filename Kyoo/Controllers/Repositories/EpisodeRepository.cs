using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kyoo.Models;
using Kyoo.Models.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kyoo.Controllers
{
	public class EpisodeRepository : IEpisodeRepository
	{
		private readonly DatabaseContext _database;
		private readonly IServiceProvider _serviceProvider;


		public EpisodeRepository(DatabaseContext database, IServiceProvider serviceProvider)
		{
			_database = database;
			_serviceProvider = serviceProvider;
		}
		
		public void Dispose()
		{
			_database.Dispose();
		}

		public ValueTask DisposeAsync()
		{
			return _database.DisposeAsync();
		}
		
		public async Task<Episode> Get(int id)
		{
			return await _database.Episodes.FirstOrDefaultAsync(x => x.ID == id);
		}

		public Task<Episode> Get(string slug)
		{
			int sIndex = slug.IndexOf("-s", StringComparison.Ordinal);
			int eIndex = slug.IndexOf("-e", StringComparison.Ordinal);
			if (sIndex == -1 || eIndex == -1 || eIndex < sIndex)
				throw new InvalidOperationException("Invalid episode slug. Format: {showSlug}-s{seasonNumber}-e{episodeNumber}");
			string showSlug = slug.Substring(0, sIndex);
			if (!int.TryParse(slug.Substring(sIndex + 2), out int seasonNumber))
				throw new InvalidOperationException("Invalid episode slug. Format: {showSlug}-s{seasonNumber}-e{episodeNumber}");
			if (!int.TryParse(slug.Substring(eIndex + 2), out int episodeNumber))
				throw new InvalidOperationException("Invalid episode slug. Format: {showSlug}-s{seasonNumber}-e{episodeNumber}");
			return Get(showSlug, seasonNumber, episodeNumber);
		}
		
		public async Task<Episode> Get(string showSlug, int seasonNumber, int episodeNumber)
		{
			return await _database.Episodes.FirstOrDefaultAsync(x => x.Show.Slug == showSlug 
			                                                         && x.SeasonNumber == seasonNumber
			                                                         && x.EpisodeNumber == episodeNumber);
		}
		
		public async Task<ICollection<Episode>> Search(string query)
		{
			return await _database.Episodes
				.Where(x => EF.Functions.Like(x.Title, $"%{query}%"))
				.Take(20)
				.ToListAsync();
		}

		public async Task<ICollection<Episode>> GetAll()
		{
			return await _database.Episodes.ToListAsync();
		}

		public async Task<int> Create(Episode obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));
			
			await Validate(obj);
			_database.Entry(obj).State = EntityState.Added;
			if (obj.ExternalIDs != null)
				foreach (MetadataID entry in obj.ExternalIDs)
					_database.Entry(entry).State = EntityState.Added;
			if (obj.Tracks != null)
				foreach (Track entry in obj.Tracks)
					_database.Entry(entry).State = EntityState.Added;
			
			try
			{
				await _database.SaveChangesAsync();
			}
			catch (DbUpdateException ex)
			{
				if (Helper.IsDuplicateException(ex))
					throw new DuplicatedItemException($"Trying to insert a duplicated episode (slug {obj.Slug} already exists).");
				throw;
			}
			return obj.ID;
		}
		
		public async Task<int> CreateIfNotExists(Episode obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			Episode old = await Get(obj.Slug);
			if (old != null)
				return old.ID;
			try
			{
				return await Create(obj);
			}
			catch (DuplicatedItemException)
			{
				old = await Get(obj.Slug);
				if (old == null)
					throw new SystemException("Unknown database state.");
				return old.ID;
			}
		}

		public async Task Edit(Episode edited, bool resetOld)
		{
			if (edited == null)
				throw new ArgumentNullException(nameof(edited));
			
			Episode old = await Get(edited.Slug);

			if (old == null)
				throw new ItemNotFound($"No episode found with the slug {edited.Slug}.");
			
			if (resetOld)
				Utility.Nullify(old);
			Utility.Merge(old, edited);

			await Validate(old);
			await _database.SaveChangesAsync();
		}

		private async Task Validate(Episode obj)
		{
			if (obj.ShowID <= 0)
				throw new InvalidOperationException($"Can't store an episode not related to any show (showID: {obj.ShowID}).");

			if (obj.ExternalIDs != null)
			{
				obj.ExternalIDs = (await Task.WhenAll(obj.ExternalIDs.Select(async x =>
				{
					using IServiceScope serviceScope = _serviceProvider.CreateScope();
					await using IProviderRepository providers = serviceScope.ServiceProvider.GetService<IProviderRepository>();

					x.ProviderID = await providers.CreateIfNotExists(x.Provider);
					return x;
				}))).ToList();
			}
		}
		
		public async Task Delete(Episode obj)
		{
			_database.Episodes.Remove(obj);
			await _database.SaveChangesAsync();
		}
		
		public async Task<ICollection<Episode>> GetEpisodes(int showID, int seasonNumber)
		{
			return await _database.Episodes.Where(x => x.ShowID == showID
			                                     && x.SeasonNumber == seasonNumber).ToListAsync();
		}

		public async Task<ICollection<Episode>> GetEpisodes(string showSlug, int seasonNumber)
		{
			return await _database.Episodes.Where(x => x.Show.Slug == showSlug
			                                           && x.SeasonNumber == seasonNumber).ToListAsync();
		}

		public async Task<ICollection<Episode>> GetEpisodes(int seasonID)
		{
			return await _database.Episodes.Where(x => x.SeasonID == seasonID).ToListAsync();
		}
	}
}