﻿using System.Collections.Generic;
using System.Threading.Tasks;
using LetPortal.Core.Persistences;
using LetPortal.Portal.Entities.Components;
using LetPortal.Portal.Models.Shared;

namespace LetPortal.Portal.Repositories.Components
{
    public interface IChartRepository : IGenericRepository<Chart>
    {
        Task<IEnumerable<ShortEntityModel>> GetShortCharts(string keyWord = null);
    }
}