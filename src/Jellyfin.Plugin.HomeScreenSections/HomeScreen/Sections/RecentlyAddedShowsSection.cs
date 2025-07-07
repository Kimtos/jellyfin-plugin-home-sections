using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections
{
    
    
    
    public class RecentlyAddedShowsSection : IHomeScreenSection
    {
        
        public string? Section => "RecentlyAddedShows";

        
        public string? DisplayText { get; set; } = " Séries ajoutées récemment";

        
        public int? Limit => 1;

        
        public string? Route => "tvshows";

        
        public string? AdditionalData { get; set; } = "tvshows";

        public object? OriginalPayload { get; set; } = null;
        
        private readonly IUserViewManager m_userViewManager;
        private readonly IUserManager m_userManager;
        private readonly ILibraryManager m_libraryManager;
        private readonly IDtoService m_dtoService;

        
        
        
        
        
        
        public RecentlyAddedShowsSection(IUserViewManager userViewManager,
            IUserManager userManager,
            ILibraryManager libraryManager,
            IDtoService dtoService)
        {
            m_userViewManager = userViewManager;
            m_userManager = userManager;
            m_libraryManager = libraryManager;
            m_dtoService = dtoService;
        }

        
        public QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
        {
            User? user = m_userManager.GetUserById(payload.UserId);

            DtoOptions? dtoOptions = new DtoOptions
            {
                Fields = new List<ItemFields>
                {
                    ItemFields.PrimaryImageAspectRatio,
                    ItemFields.Path
                }
            };

            dtoOptions.ImageTypeLimit = 1;
            dtoOptions.ImageTypes = new List<ImageType>
            {
                ImageType.Thumb,
                ImageType.Backdrop,
                ImageType.Primary,
            };

            List<BaseItem> episodes = m_libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                OrderBy = new[] { (ItemSortBy.DateCreated, SortOrder.Descending) },
                DtoOptions = new DtoOptions
                    { Fields = new[] { ItemFields.SeriesPresentationUniqueKey }, EnableImages = false }
            });
            
            List<BaseItem> series = episodes
                .Where(x => !x.IsUnaired && !x.IsVirtualItem)
                .Select(x => (x.FindParent<Series>(), (x as Episode)?.DateCreated))
                .GroupBy(x => x.Item1)
                .Select(x => (x.Key, x.Max(y => y.DateCreated)))
                .OrderByDescending(x => x.Item2)
                .Select(x => x.Key as BaseItem)
                .Take(16)
                .ToList();

            return new QueryResult<BaseItemDto>(Array.ConvertAll(series.ToArray(),
                i => m_dtoService.GetBaseItemDto(i, dtoOptions, user)));
        }

        
        public IHomeScreenSection CreateInstance(Guid? userId, IEnumerable<IHomeScreenSection>? otherInstances = null)
        {
            User? user = m_userManager.GetUserById(userId ?? Guid.Empty);

            Folder? folder = m_libraryManager.GetUserRootFolder()
                .GetChildren(user, true)
                .OfType<Folder>()
                .Select(x => x as ICollectionFolder)
                .Where(x => x != null)
                .FirstOrDefault(x => x!.CollectionType == CollectionType.tvshows) as Folder;

            BaseItemDto? originalPayload = null;
            if (folder != null)
            {
                DtoOptions dtoOptions = new DtoOptions();
                dtoOptions.Fields =
                    [..dtoOptions.Fields, ItemFields.PrimaryImageAspectRatio, ItemFields.DisplayPreferencesId];

                originalPayload = Array.ConvertAll(new[] { folder }, i => m_dtoService.GetBaseItemDto(i, dtoOptions, user)).First();
            }

            return new RecentlyAddedShowsSection(m_userViewManager, m_userManager, m_libraryManager, m_dtoService)
            {
                AdditionalData = AdditionalData,
                DisplayText = DisplayText,
                OriginalPayload = originalPayload
            };
        }
        
        public HomeScreenSectionInfo GetInfo()
        {
            return new HomeScreenSectionInfo
            {
                Section = Section,
                DisplayText = DisplayText,
                AdditionalData = AdditionalData,
                Route = Route,
                Limit = Limit ?? 1,
                OriginalPayload = OriginalPayload,
                ViewMode = SectionViewMode.Landscape
            };
        }
    }
}
