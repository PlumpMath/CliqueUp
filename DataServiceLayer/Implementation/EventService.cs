﻿using System;
using System.Collections.Generic;
using System.Linq;
using CliqueUpModel.Model;
using CliqueUpModel.Contract;
using DataServiceLayer.Helper;
using Google.Maps.Geocoding;
using Models.Exception;
using Models.Model;

namespace DataServiceLayer.Implementation
{
    public class EventService : IEventService
    {
        public CategoryEvent CreateEvent(string title, string description, IEnumerable<string> categories, DateTime start, DateTime end, double lat, double lon)
        {
            var dbContext = new CliqueUpContext();
            var newEvent = new CategoryEvent
            {
                Id = Guid.NewGuid(),
                Categories = GetCategories(dbContext, categories),
                CreateOn = DateTime.Now,
                Description = description,
                Title = title,
                StartTime = start,
                EndTime = end,
                Latitude = lat,
                Longitude = lon,
                IsActive = true,
                DisabledOn = null,
            };

            dbContext.CategoryEvents.Add(newEvent);
            dbContext.SaveChanges();
            return newEvent;
        }

        public bool OpenEvent(Guid userId, Guid eventId)
        {

            throw new NotImplementedException();
        }

        public bool CloseEvent(Guid userId, Guid eventId)
        {
            throw new NotImplementedException();
        }

        public Coordinate ReverseGeocode(string locationSearch)
        {
            try
            {
                var request = new GeocodingRequest { Address = locationSearch, Sensor=true};
                var response = GeocodingService.GetResponse(request);
                var latlon = response.Results.First().Geometry.Location;

                return new Coordinate { Latitude = latlon.Latitude, Longitude = latlon.Longitude };
            }

            catch (Exception exception)
            {
                throw new InvalidGeocodeException("Reverse geocode failed. See InnerException for details", exception);
            }
        }

        public IEnumerable<CategoryEvent> SearchEvents(string searchQuery, string location, int searchRadiusMiles)
        {
            var coord = ReverseGeocode(location);
            return this.SearchEvents(searchQuery, coord.Latitude, coord.Longitude, searchRadiusMiles);
        }

        public IEnumerable<CategoryEvent> SearchEvents(string searchQuery, double baseLatitude, double baseLongitude, int searchRadiusMiles)
        {
            searchQuery = searchQuery == null ? null : searchQuery.Trim().ToLower();
            var splitCategories = searchQuery == null ? 
                null : searchQuery.Split(' ').Select(s => s.Trim()).Where(s => s.StartsWith("#"));

            var dbContext = new CliqueUpContext();
            var events =
               (from e in dbContext.CategoryEvents.Include("Categories")
                where searchQuery == null || e.Description.Contains(searchQuery)
                where splitCategories == null || e.Categories.Any(c => splitCategories.Any(sc => sc == c.Description))
                select e)
                .ToList()
                .Where(e => searchRadiusMiles >= GeoMath.Distance(baseLatitude, baseLongitude, e.Latitude, e.Longitude, GeoMath.MeasureUnits.Miles));

            return events;
        }

        public EventMessage PostEventMessage(Guid userid, Guid eventId, string messageText)
        {
            var dbContext = new CliqueUpContext();
            var newMessage = new EventMessage
            {
                Id = Guid.NewGuid(),
                UserId = userid,
                EventId = eventId,
                Text = messageText
            };

            dbContext.EventMessages.Add(newMessage);
            dbContext.SaveChanges();

            return newMessage;
        }

        public void JoinEvent(Guid userid, Guid eventid)
        {
            var dbContext = new CliqueUpContext();
            var eventUser = new EventUser()
                {
                    Id = Guid.Empty,
                    UserId = userid,
                    EventId = eventid
                };

            dbContext.EventUsers.Add(eventUser);
            dbContext.SaveChanges();
        }

        public List<Category> GetCategories(CliqueUpContext cliqueUpContext, IEnumerable<string> categories)
        {
            return categories.Select(category => GetOrCreateCategoryIfNotExists(cliqueUpContext, category)).ToList();
        }

        public Category GetOrCreateCategoryIfNotExists(CliqueUpContext cliqueUpContext, string category)
        {
            category = category.Trim().ToLower();
            var dbCategory = cliqueUpContext
                .EventCategories
                .SingleOrDefault(eventCategory => eventCategory.Description == category);

            if (dbCategory == null)
            {
                dbCategory = new Category()
                    {
                        Id = Guid.NewGuid(),
                        Description = category
                    };
                cliqueUpContext.EventCategories.Add(dbCategory);
                // We should do this AFTER retrieving everything so we can keep
                // all the creations in a single transaction.
                cliqueUpContext.SaveChanges(); 
            }

            return dbCategory;
        }
    }
}
