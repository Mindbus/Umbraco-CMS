﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Components;
using Umbraco.Core.Logging;
using Umbraco.Core.Services.Changes;
using Umbraco.Web.Services;
using Content = Umbraco.Core.Models.Content;
using ApplicationTree = Umbraco.Core.Models.ApplicationTree;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// Installs listeners on service events in order to refresh our caches.
    /// </summary>
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    [RequiredComponent(typeof(IUmbracoCoreComponent))] // runs before every other IUmbracoCoreComponent!
    public class CacheRefresherComponent : UmbracoComponentBase, IUmbracoCoreComponent
    {
        private static readonly ConcurrentDictionary<IEventDefinition, MethodInfo> FoundHandlers = new ConcurrentDictionary<IEventDefinition, MethodInfo>();
        private List<Action> _unbinders;

        public CacheRefresherComponent()
        { }

        // for tests
        public CacheRefresherComponent(bool supportUnbinding)
        {
            if (supportUnbinding)
                _unbinders = new List<Action>();
        }

        public void Initialize()
        {
            Current.Logger.Info<CacheRefresherComponent>("Initializing Umbraco internal event handlers for cache refreshing.");

            // bind to application tree events
            Bind(() => ApplicationTreeService.Deleted += ApplicationTreeService_Deleted,
                () => ApplicationTreeService.Deleted -= ApplicationTreeService_Deleted);
            Bind(() => ApplicationTreeService.Updated += ApplicationTreeService_Updated,
                () => ApplicationTreeService.Updated -= ApplicationTreeService_Updated);
            Bind(() => ApplicationTreeService.New += ApplicationTreeService_New,
                () => ApplicationTreeService.New -= ApplicationTreeService_New);

            // bind to application events
            Bind(() => SectionService.Deleted += SectionService_Deleted,
                () => SectionService.Deleted -= SectionService_Deleted);
            Bind(() => SectionService.New += SectionService_New,
                () => SectionService.New -= SectionService_New);

            // bind to user and user type events
            Bind(() => UserService.SavedUserType += UserService_SavedUserType,
                () => UserService.SavedUserType -= UserService_SavedUserType);
            Bind(() => UserService.DeletedUserType += UserService_DeletedUserType,
                () => UserService.DeletedUserType -= UserService_DeletedUserType);
            Bind(() => UserService.SavedUser += UserService_SavedUser,
                () => UserService.SavedUser -= UserService_SavedUser);
            Bind(() => UserService.DeletedUser += UserService_DeletedUser,
                () => UserService.DeletedUser -= UserService_DeletedUser);

            // bind to dictionary events
            Bind(() => LocalizationService.DeletedDictionaryItem += LocalizationService_DeletedDictionaryItem,
                () => LocalizationService.DeletedDictionaryItem -= LocalizationService_DeletedDictionaryItem);
            Bind(() => LocalizationService.SavedDictionaryItem += LocalizationService_SavedDictionaryItem,
                () => LocalizationService.SavedDictionaryItem -= LocalizationService_SavedDictionaryItem);

            // bind to data type events
            Bind(() => DataTypeService.Deleted += DataTypeService_Deleted,
                () => DataTypeService.Deleted -= DataTypeService_Deleted);
            Bind(() => DataTypeService.Saved += DataTypeService_Saved,
                () => DataTypeService.Saved -= DataTypeService_Saved);

            // bind to stylesheet events
            // fixme why not in v8?
            //Bind(() => FileService.SavedStylesheet += FileService_SavedStylesheet,
            //    () => FileService.SavedStylesheet -= FileService_SavedStylesheet);
            //Bind(() => FileService.DeletedStylesheet += FileService_DeletedStylesheet,
            //    () => FileService.DeletedStylesheet -= FileService_DeletedStylesheet);

            // bind to domain events
            Bind(() => DomainService.Saved += DomainService_Saved,
                () => DomainService.Saved -= DomainService_Saved);
            Bind(() => DomainService.Deleted += DomainService_Deleted,
                () => DomainService.Deleted -= DomainService_Deleted);

            // bind to language events
            Bind(() => LocalizationService.SavedLanguage += LocalizationService_SavedLanguage,
                () => LocalizationService.SavedLanguage -= LocalizationService_SavedLanguage);
            Bind(() => LocalizationService.DeletedLanguage += LocalizationService_DeletedLanguage,
                () => LocalizationService.DeletedLanguage -= LocalizationService_DeletedLanguage);

            // bind to content type events
            Bind(() => ContentTypeService.Changed += ContentTypeService_Changed,
                () => ContentTypeService.Changed -= ContentTypeService_Changed);
            Bind(() => MediaTypeService.Changed += ContentTypeService_Changed,
                () => MediaTypeService.Changed -= ContentTypeService_Changed);
            Bind(() => MemberTypeService.Changed += ContentTypeService_Changed,
                () => MemberTypeService.Changed -= ContentTypeService_Changed);

            // bind to permission events
            // fixme see v7 events? the PermissionNew/Updated/Deleted are not supported here?
            Bind(() => PermissionRepository<IContent>.AssignedPermissions += PermissionRepository_AssignedPermissions,
                () => PermissionRepository<IContent>.AssignedPermissions -= PermissionRepository_AssignedPermissions);

            // bind to template events
            Bind(() => FileService.SavedTemplate += FileService_SavedTemplate,
                () => FileService.SavedTemplate -= FileService_SavedTemplate);
            Bind(() => FileService.DeletedTemplate += FileService_DeletedTemplate,
                () => FileService.DeletedTemplate -= FileService_DeletedTemplate);

            // bind to macro events
            Bind(() => MacroService.Saved += MacroService_Saved,
                () => MacroService.Saved -= MacroService_Saved);
            Bind(() => MacroService.Deleted += MacroService_Deleted,
                () => MacroService.Deleted -= MacroService_Deleted);

            // bind to member events
            Bind(() => MemberService.Saved += MemberService_Saved,
                () => MemberService.Saved -= MemberService_Saved);
            Bind(() => MemberService.Deleted += MemberService_Deleted,
                () => MemberService.Deleted -= MemberService_Deleted);
            Bind(() => MemberGroupService.Saved += MemberGroupService_Saved,
                () => MemberGroupService.Saved -= MemberGroupService_Saved);
            Bind(() => MemberGroupService.Deleted += MemberGroupService_Deleted,
                () => MemberGroupService.Deleted -= MemberGroupService_Deleted);

            // bind to media events - handles all media changes
            Bind(() => MediaService.TreeChanged += MediaService_Changed,
                () => MediaService.TreeChanged -= MediaService_Changed);

            // bind to content events
            Bind(() => ContentService.Saved += ContentService_Saved, // needed for permissions
                () => ContentService.Saved -= ContentService_Saved);
            Bind(() => ContentService.Copied += ContentService_Copied, // needed for permissions
                () => ContentService.Copied -= ContentService_Copied);
            Bind(() => ContentService.TreeChanged += ContentService_Changed,// handles all content changes
                () => ContentService.TreeChanged -= ContentService_Changed);

            // bind to public access events
            Bind(() => PublicAccessService.Saved += PublicAccessService_Saved,
                () => PublicAccessService.Saved -= PublicAccessService_Saved);
            Bind(() => PublicAccessService.Deleted += PublicAccessService_Deleted,
                () => PublicAccessService.Deleted -= PublicAccessService_Deleted);

            // bind to relation type events
            Bind(() => RelationService.SavedRelationType += RelationService_SavedRelationType,
                () => RelationService.SavedRelationType -= RelationService_SavedRelationType);
            Bind(() => RelationService.DeletedRelationType += RelationService_DeletedRelationType,
                () => RelationService.DeletedRelationType -= RelationService_DeletedRelationType);
        }

        #region Events binding and handling

        private void Bind(Action binder, Action unbinder)
        {
            // bind now
            binder();

            // and register unbinder for later, if needed
            _unbinders?.Add(unbinder);
        }

        // for tests
        internal void Unbind()
        {
            if (_unbinders == null)
                throw new NotSupportedException();
            foreach (var unbinder in _unbinders)
                unbinder();
            _unbinders = null;
        }

        internal static MethodInfo FindHandler(IEventDefinition eventDefinition)
        {
            var name = eventDefinition.Sender.GetType().Name + "_" + eventDefinition.EventName;

            return FoundHandlers.GetOrAdd(eventDefinition, _ => CandidateHandlers.Value.FirstOrDefault(x => x.Name == name));
        }

        private static readonly Lazy<MethodInfo[]> CandidateHandlers = new Lazy<MethodInfo[]>(() =>
        {
            var underscore = new[] { '_' };

            return typeof(CacheRefresherComponent)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(x =>
                {
                    if (x.Name.Contains("_") == false) return null;

                    var parts = x.Name.Split(underscore, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (parts != 2) return null;

                    var parameters = x.GetParameters();
                    if (parameters.Length != 2) return null;
                    if (typeof(EventArgs).IsAssignableFrom(parameters[1].ParameterType) == false) return null;
                    return x;
                })
                .WhereNotNull()
                .ToArray();
        });

        internal static void HandleEvents(IEnumerable<IEventDefinition> events)
        {
            foreach (var e in events)
            {
                var handler = FindHandler(e);
                if (handler == null) continue;

                handler.Invoke(null, new[] { e.Sender, e.Args });
            }
        }

        #endregion

        #region PublicAccessService

        private static void PublicAccessService_Saved(IPublicAccessService sender, SaveEventArgs<PublicAccessEntry> e)
        {
            DistributedCache.Instance.RefreshPublicAccess();
        }

        private static void PublicAccessService_Deleted(IPublicAccessService sender, DeleteEventArgs<PublicAccessEntry> e)
        {
            DistributedCache.Instance.RefreshPublicAccess();
        }

        #endregion

        #region ContentService

        /// <summary>
        /// Handles cache refreshing for when content is copied
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// When an entity is copied new permissions may be assigned to it based on it's parent, if that is the
        /// case then we need to clear all user permissions cache.
        /// </remarks>
        private static void ContentService_Copied(IContentService sender, CopyEventArgs<IContent> e)
        {
            //check if permissions have changed
            var permissionsChanged = ((Content)e.Copy).WasPropertyDirty("PermissionsChanged");
            if (permissionsChanged)
            {
                DistributedCache.Instance.RefreshAllUserPermissionsCache();
            }
        }

        /// <summary>
        /// Handles cache refreshing for when content is saved (not published)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// When an entity is saved we need to notify other servers about the change in order for the Examine indexes to
        /// stay up-to-date for unpublished content.
        ///
        /// When an entity is created new permissions may be assigned to it based on it's parent, if that is the
        /// case then we need to clear all user permissions cache.
        /// </remarks>
        private static void ContentService_Saved(IContentService sender, SaveEventArgs<IContent> e)
        {
            var clearUserPermissions = false;
            foreach (var entity in e.SavedEntities)
            {
                //check if it is new
                if (entity.IsNewEntity())
                {
                    //check if permissions have changed
                    var permissionsChanged = ((Content)entity).WasPropertyDirty("PermissionsChanged");
                    if (permissionsChanged)
                    {
                        clearUserPermissions = true;
                    }
                }
            }

            if (clearUserPermissions)
            {
                DistributedCache.Instance.RefreshAllUserPermissionsCache();
            }
        }

        private static void ContentService_Changed(IContentService sender, TreeChange<IContent>.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentCache(args.Changes.ToArray());
        }

        #endregion

        #region ApplicationTreeService

        private static void ApplicationTreeService_New(ApplicationTree sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationTreeCache();
        }

        private static void ApplicationTreeService_Updated(ApplicationTree sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationTreeCache();
        }

        private static void ApplicationTreeService_Deleted(ApplicationTree sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationTreeCache();
        }
        #endregion

        #region Application event handlers

        private static void SectionService_New(Section sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationCache();
        }

        private static void SectionService_Deleted(Section sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationCache();
        }

        #endregion

        #region UserService / UserType

        private static void UserService_DeletedUserType(IUserService sender, DeleteEventArgs<IUserType> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveUserTypeCache(entity.Id);
        }

        private static void UserService_SavedUserType(IUserService sender, SaveEventArgs<IUserType> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshUserTypeCache(entity.Id);
        }

        #endregion

        #region LocalizationService / Dictionary

        private static void LocalizationService_SavedDictionaryItem(ILocalizationService sender, SaveEventArgs<IDictionaryItem> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshDictionaryCache(entity.Id);
        }

        private static void LocalizationService_DeletedDictionaryItem(ILocalizationService sender, DeleteEventArgs<IDictionaryItem> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveDictionaryCache(entity.Id);
        }

        #endregion

        #region DataTypeService

        private static void DataTypeService_Saved(IDataTypeService sender, SaveEventArgs<IDataTypeDefinition> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshDataTypeCache(entity);
        }

        private static void DataTypeService_Deleted(IDataTypeService sender, DeleteEventArgs<IDataTypeDefinition> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveDataTypeCache(entity);
        }

        #endregion

        #region DomainService

        private static void DomainService_Saved(IDomainService sender, SaveEventArgs<IDomain> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshDomainCache(entity);
        }

        private static void DomainService_Deleted(IDomainService sender, DeleteEventArgs<IDomain> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveDomainCache(entity);
        }

        #endregion

        #region LocalizationService / Language

        /// <summary>
        /// Fires when a langauge is deleted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void LocalizationService_DeletedLanguage(ILocalizationService sender, DeleteEventArgs<ILanguage> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveLanguageCache(entity);
        }

        /// <summary>
        /// Fires when a langauge is saved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void LocalizationService_SavedLanguage(ILocalizationService sender, SaveEventArgs<ILanguage> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshLanguageCache(entity);
        }

        #endregion

        #region Content|Media|MemberTypeService

        private static void ContentTypeService_Changed(IContentTypeService sender, ContentTypeChange<IContentType>.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentTypeCache(args.Changes.ToArray());
        }

        private static void ContentTypeService_Changed(IMediaTypeService sender, ContentTypeChange<IMediaType>.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentTypeCache(args.Changes.ToArray());
        }

        private static void ContentTypeService_Changed(IMemberTypeService sender, ContentTypeChange<IMemberType>.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentTypeCache(args.Changes.ToArray());
        }

        #endregion

        #region UserService & PermissionRepository

        private static void PermissionRepository_AssignedPermissions(PermissionRepository<IContent> sender, SaveEventArgs<EntityPermission> e)
        {
            var userIds = e.SavedEntities.Select(x => x.UserId).Distinct();
            foreach (var id in userIds)
                DistributedCache.Instance.RefreshUserPermissionsCache(id);
        }

        private static void UserService_SavedUser(IUserService sender, SaveEventArgs<IUser> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshUserCache(entity.Id);
        }

        private static void UserService_DeletedUser(IUserService sender, DeleteEventArgs<IUser> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveUserCache(entity.Id);
        }

        #endregion

        #region FileService / Template

        /// <summary>
        /// Removes cache for template
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FileService_DeletedTemplate(IFileService sender, DeleteEventArgs<ITemplate> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveTemplateCache(entity.Id);
        }

        /// <summary>
        /// Refresh cache for template
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void FileService_SavedTemplate(IFileService sender, SaveEventArgs<ITemplate> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshTemplateCache(entity.Id);
        }

        #endregion

        #region MacroService

        private static void MacroService_Deleted(IMacroService sender, DeleteEventArgs<IMacro> e)
        {
            foreach (var entity in e.DeletedEntities)
                DistributedCache.Instance.RemoveMacroCache(entity);
        }

        private static void MacroService_Saved(IMacroService sender, SaveEventArgs<IMacro> e)
        {
            foreach (var entity in e.SavedEntities)
                DistributedCache.Instance.RefreshMacroCache(entity);
        }

        #endregion

        #region MediaService

        private static void MediaService_Changed(IMediaService sender, TreeChange<IMedia>.EventArgs args)
        {
            DistributedCache.Instance.RefreshMediaCache(args.Changes.ToArray());
        }

        #endregion

        #region MemberService

        private static void MemberService_Deleted(IMemberService sender, DeleteEventArgs<IMember> e)
        {
            DistributedCache.Instance.RemoveMemberCache(e.DeletedEntities.ToArray());
        }

        private static void MemberService_Saved(IMemberService sender, SaveEventArgs<IMember> e)
        {
            DistributedCache.Instance.RefreshMemberCache(e.SavedEntities.ToArray());
        }

        #endregion

        #region MemberGroupService

        private static void MemberGroupService_Deleted(IMemberGroupService sender, DeleteEventArgs<IMemberGroup> e)
        {
            foreach (var m in e.DeletedEntities.ToArray())
            {
                DistributedCache.Instance.RemoveMemberGroupCache(m.Id);
            }
        }

        private static void MemberGroupService_Saved(IMemberGroupService sender, SaveEventArgs<IMemberGroup> e)
        {
            foreach (var m in e.SavedEntities.ToArray())
            {
                DistributedCache.Instance.RemoveMemberGroupCache(m.Id);
            }
        }
        #endregion

        #region RelationType

        private static void RelationService_SavedRelationType(IRelationService sender, SaveEventArgs<IRelationType> args)
        {
            var dc = DistributedCache.Instance;
            foreach (var e in args.SavedEntities)
                dc.RefreshRelationTypeCache(e.Id);
        }

        private static void RelationService_DeletedRelationType(IRelationService sender, DeleteEventArgs<IRelationType> args)
        {
            var dc = DistributedCache.Instance;
            foreach (var e in args.DeletedEntities)
                dc.RemoveRelationTypeCache(e.Id);
        }
        
        #endregion
    }
}