﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Applications.Trees;
using Bing.Datas.Queries.Trees;
using Bing.Domains.Repositories;
using Bing.Exceptions;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Webs.Controllers.Trees
{
    /// <summary>
    /// 树型表格控制器
    /// </summary>
    /// <typeparam name="TTreeResult">树型结果</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQuery">父标识类型</typeparam>
    public abstract class TreeTableControllerBase<TTreeResult, TDto, TQuery> : TreeTableControllerBase<TTreeResult, TDto, TQuery, Guid?>
        where TTreeResult : class, new()
        where TDto : class, ITreeNode, new()
        where TQuery : class, ITreeQueryParameter, new()
    {
        /// <summary>
        /// 初始化一个<see cref="TreeTableControllerBase{TTreeResult,TDto,TQuery}"/>类型的实例
        /// </summary>
        /// <param name="service">树型服务</param>
        protected TreeTableControllerBase(ITreeService<TDto, TQuery, Guid?> service) : base(service)
        {
        }
    }

    /// <summary>
    /// 树型表格控制器
    /// </summary>
    /// <typeparam name="TTreeResult">树型结果</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQuery">查询参数类型</typeparam>
    /// <typeparam name="TParentId">父标识类型</typeparam>
    public abstract class TreeTableControllerBase<TTreeResult, TDto, TQuery, TParentId> : ControllerBase<TDto, TQuery, TParentId>
        where TTreeResult : class, new()
        where TDto : class, ITreeNode, new()
        where TQuery : class, ITreeQueryParameter<TParentId>, new()
    {
        /// <summary>
        /// 树型服务
        /// </summary>
        private readonly ITreeService<TDto, TQuery, TParentId> _service;

        /// <summary>
        /// 初始化一个<see cref="TreeTableControllerBase{TTreeResult,TDto,TQuery,TParentId}"/>类型的实例
        /// </summary>
        /// <param name="service">树型服务</param>
        protected TreeTableControllerBase(ITreeService<TDto, TQuery, TParentId> service) : base(service)
        {
            _service = service;
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <remarks>
        /// 调用范例: 
        /// GET
        /// /api/role?name=a
        /// </remarks>
        /// <param name="query">查询参数</param>
        [HttpGet]
        public virtual async Task<IActionResult> QueryAsync([FromQuery]TQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            QueryBefore(query);
            InitParam(query);
            PagerList<TTreeResult> result;
            switch (GetOperation(query))
            {
                case LoadOperation.FirstLoad:
                    result = await FirstLoad(query);
                    break;
                case LoadOperation.LoadChild:
                    result = await LoadChildren(query);
                    break;
                default:
                    result = await Search(query);
                    break;
            }

            return Success(result);
        }

        /// <summary>
        /// 查询前操作
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual void QueryBefore(TQuery query) { }

        /// <summary>
        /// 初始化参数
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual void InitParam(TQuery query)
        {
            if (query.Order.IsEmpty())
            {
                query.Order = "SortId";
            }

            query.Path = null;
            if (GetOperation(query) == LoadOperation.LoadChild)
            {
                return;
            }

            query.ParentId = default(TParentId);
        }

        /// <summary>
        /// 获取操作
        /// </summary>
        /// <param name="query">查询参数</param>
        protected LoadOperation? GetOperation(TQuery query)
        {
            var operation = Request.Query["operation"].SafeString().ToLower();
            if (operation == "loadchild")
            {
                return LoadOperation.LoadChild;
            }

            return query.IsSearch() ? LoadOperation.Search : LoadOperation.FirstLoad;
        }

        /// <summary>
        /// 首次加载
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<PagerList<TTreeResult>> FirstLoad(TQuery query)
        {
            if (GetLoadMode() == LoadMode.Sync)
            {
                return await SyncFirstLoad(query);
            }

            return await AsyncFirstLoad(query);
        }

        /// <summary>
        /// 同步首次查询
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<PagerList<TTreeResult>> SyncFirstLoad(TQuery query)
        {
            var data = await Query(query);
            return ToResult(data);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询参数</param>
        private async Task<List<TDto>> Query(TQuery query)
        {
            var data = await _service.QueryAsync(query);
            ProcessData(data, query);
            return data;
        }

        /// <summary>
        /// 数据处理
        /// </summary>
        /// <param name="data">数据列表</param>
        /// <param name="query">查询参数</param>
        protected virtual void ProcessData(List<TDto> data, TQuery query)
        {
        }

        /// <summary>
        /// 转换为树型结果
        /// </summary>
        /// <param name="data">数据列表</param>
        /// <param name="async">是否异步</param>
        protected abstract PagerList<TTreeResult> ToResult(List<TDto> data, bool async = false);

        /// <summary>
        /// 异步首次加载
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<PagerList<TTreeResult>> AsyncFirstLoad(TQuery query)
        {
            query.Level = 1;
            var data = await _service.PagerQueryAsync(query);
            ProcessData(data.Data, query);
            return ToResult(data.Data, true);
        }

        /// <summary>
        /// 加载子节点
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<PagerList<TTreeResult>> LoadChildren(TQuery query)
        {
            if (query.ParentId == null)
            {
                throw new Warning("父节点标识为空，加载节点失败");
            }

            if (GetLoadMode() == LoadMode.Async)
            {
                return await AsyncLoadChildren(query);
            }

            return await SyncLoadChildren(query);
        }

        /// <summary>
        /// 异步加载子节点
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<PagerList<TTreeResult>> AsyncLoadChildren(TQuery query)
        {
            var queryParam = await GetAsyncLoadChildrenQuery(query);
            var data = await Query(queryParam);
            return ToResult(data, true);
        }

        /// <summary>
        /// 获取异步加载子节点查询参数
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual Task<TQuery> GetAsyncLoadChildrenQuery(TQuery query)
        {
            query.Level = null;
            query.Path = null;
            return Task.FromResult(query);
        }

        /// <summary>
        /// 同步加载子节点
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<PagerList<TTreeResult>> SyncLoadChildren(TQuery query)
        {
            var parentId = query.ParentId.SafeString();
            var queryParam = await GetSyncLoadChildrenQuery(query);
            var data = await _service.QueryAsync(queryParam);
            data.RemoveAll(t => t.Id == parentId);
            ProcessData(data, query);
            return ToResult(data);
        }

        /// <summary>
        /// 获取同步加载子节点查询参数
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<TQuery> GetSyncLoadChildrenQuery(TQuery query)
        {
            var parent = await _service.GetByIdAsync(query.ParentId);
            query.Path = parent.Path;
            query.Level = null;
            query.ParentId = default(TParentId);
            return query;
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="query">查询参数</param>
        protected virtual async Task<PagerList<TTreeResult>> Search(TQuery query)
        {
            var data = await _service.QueryAsync(query);
            var ids = data.GetMissingParentIds();
            var list = await _service.GetByIdsAsync(ids.Join());
            data.AddRange(list);
            ProcessData(data, query);
            if (GetLoadMode() == LoadMode.Async)
            {
                return ToResult(data, true);
            }

            return ToResult(data);
        }
    }
}
