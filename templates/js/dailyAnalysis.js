
var DailyAnalysis = (function() {
    var queryConfig = [];
    var sortColumn = '';
    var sortDirection = 'DESC';
    var chartInstance = null;

    var ErrorHandler = {
        show: function(message, type) {
            type = type || 'error';
            var alertClass = type === 'success' ? 'alert-success' : 
                           type === 'warning' ? 'alert-warning' : 'alert-danger';
            
            var alertHtml = '<div class="alert ' + alertClass + ' alert-dismissible fade show" role="alert">' +
                           message +
                           '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>';
            
            $('#panelContent').prepend(alertHtml);
            setTimeout(function() {
                $('.alert').fadeOut();
            }, 5000);
        },
        
        handleAjaxError: function(xhr, status, error) {
            var message = '请求失败';
            if (xhr.status === 404) {
                message = '服务接口不存在';
            } else if (xhr.status === 500) {
                message = '服务器内部错误';
            } else if (error) {
                message = error;
            }
            ErrorHandler.show(message, 'error');
            console.error('AJAX Error:', status, error);
        },
        
        validateResponse: function(response) {
            try {
                var resp = typeof response === 'string' ? JSON.parse(response) : response;
                if (!resp.success) {
                    ErrorHandler.show(resp.error || '操作失败', 'warning');
                    return null;
                }
                return resp.data;
            } catch (e) {
                ErrorHandler.show('数据解析失败', 'error');
                console.error('Parse Error:', e);
                return null;
            }
        }
    };

    function loadQueryConfig(callback) {
        $.ajax({
            url: '/get-query-config',
            type: 'GET',
            success: function(response) {
                var data = ErrorHandler.validateResponse(response);
                if (data) {
                    queryConfig = data;
                    if (callback) callback();
                }
            },
            error: ErrorHandler.handleAjaxError
        });
    }

    function loadFieldOptions(fieldName, parentValue, callback) {
        var url = '/execute-dynamic-query?fieldName=' + encodeURIComponent(fieldName);
        if (parentValue) {
            url += '&parentValue=' + encodeURIComponent(parentValue);
        }
        
        $.ajax({
            url: url,
            type: 'GET',
            success: function(response) {
                var data = ErrorHandler.validateResponse(response);
                if (data) {
                    var select = $('#' + fieldName);
                    select.empty();
                    var config = queryConfig.find(function(c) { return c.fieldName === fieldName; });
                    if (config && config.isMultiple) {
                        data.forEach(function(item) {
                            select.append('<option value="' + htmlEncode(item.code) + '">' + htmlEncode(item.name) + '</option>');
                        });
                    } else {
                        select.append('<option value="">' + (config ? config.placeholder : '请选择') + '</option>');
                        data.forEach(function(item) {
                            select.append('<option value="' + htmlEncode(item.code) + '">' + htmlEncode(item.name) + '</option>');
                        });
                    }
                    if (callback) callback();
                }
            },
            error: ErrorHandler.handleAjaxError
        });
    }

    function htmlEncode(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;')
                  .replace(/</g, '&lt;')
                  .replace(/>/g, '&gt;')
                  .replace(/"/g, '&quot;');
    }

    function onParentFieldChanged(fieldName) {
        var config = queryConfig.find(function(c) { return c.fieldName === fieldName; });
        if (!config) return;
        
        var parentValue = $('#' + fieldName).val();
        
        queryConfig.forEach(function(childConfig) {
            if (childConfig.parentField === fieldName) {
                loadFieldOptions(childConfig.fieldName, parentValue);
            }
        });
    }

    function generateDynamicFilters() {
        var container = $('#dynamicFilters');
        container.empty();
        
        var visibleFields = queryConfig.filter(function(c) { return c.isVisible; });
        
        visibleFields.forEach(function(config) {
            var fieldHtml = '<div style="min-width: 150px; max-width: 200px;">';
            fieldHtml += '<label class="form-label text-white small">' + htmlEncode(config.displayName) + '</label>';
            
            if (config.isMultiple) {
                fieldHtml += '<select class="form-select form-select-sm" id="' + config.fieldName + '" multiple size="3" style="height: 60px;">';
                fieldHtml += '<option value="">加载中...</option>';
                fieldHtml += '</select>';
            } else {
                fieldHtml += '<select class="form-select form-select-sm" id="' + config.fieldName + '" onchange="DailyAnalysis.onParentFieldChanged(\'' + config.fieldName + '\')">';
                fieldHtml += '<option value="">' + config.placeholder + '</option>';
                fieldHtml += '</select>';
            }
            
            fieldHtml += '</div>';
            container.append(fieldHtml);
        });
    }

    function loadAllFieldOptions() {
        queryConfig.forEach(function(config) {
            if (config.isVisible) {
                loadFieldOptions(config.fieldName, '');
            }
        });
    }

    function runDailyAnalysis() {
        var startDate = $('#startDate').val();
        var endDate = $('#endDate').val();
        
        if (!startDate || !endDate) {
            ErrorHandler.show('请选择日期范围', 'warning');
            return;
        }
        
        var data = {startDate: startDate, endDate: endDate};
        
        queryConfig.forEach(function(config) {
            if (config.isVisible) {
                var value = $('#' + config.fieldName).val();
                if (config.isMultiple && Array.isArray(value)) {
                    data[config.fieldName] = value.join(',');
                } else {
                    data[config.fieldName] = value || '';
                }
            }
        });
        
        data.sortBy = sortColumn || '任务数量';
        data.sortOrder = sortDirection;
        
        $('#analysisResult').html('<div class="text-center text-white"><span class="spinner-border spinner-border-sm"></span> 加载中...</div>');
        hideChart();
        
        $.ajax({
            url: '/daily-analysis',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function(response) {
                var data = ErrorHandler.validateResponse(response);
                if (data) {
                    displayAnalysisResult(data);
                    updateStatistics(data);
                    renderChart(data);
                } else {
                    resetStatistics();
                }
            },
            error: function(xhr, status, error) {
                ErrorHandler.handleAjaxError(xhr, status, error);
                $('#analysisResult').html('<div class="alert alert-danger">加载失败</div>');
                resetStatistics();
            }
        });
    }

    function displayAnalysisResult(data) {
        if (!data || data.length === 0) {
            $('#analysisResult').html('<div class="alert alert-warning">没有数据</div>');
            return;
        }
        
        var columns = Object.keys(data[0]);
        var tableHtml = '<div class="table-responsive" style="max-height: 500px; overflow-y: auto;">';
        tableHtml += '<table class="table table-striped table-bordered table-sm bg-white">';
        tableHtml += '<thead class="table-dark sticky-top"><tr>';
        
        for (var i = 0; i < columns.length; i++) {
            var col = columns[i];
            var sortIcon = '';
            if (sortColumn === col) {
                sortIcon = sortDirection === 'ASC' ? ' ▲' : ' ▼';
            }
            tableHtml += '<th style="cursor: pointer; white-space: nowrap;" onclick="DailyAnalysis.sortByColumn(\'' + col + '\')">' + htmlEncode(col) + sortIcon + '</th>';
        }
        
        tableHtml += '</tr></thead><tbody>';
        
        for (var i = 0; i < data.length; i++) {
            tableHtml += '<tr>';
            for (var j = 0; j < columns.length; j++) {
                var value = data[i][columns[j]];
                if (columns[j] === '阳性率') {
                    tableHtml += '<td>' + (value ? value.toFixed(2) + '%' : '') + '</td>';
                } else {
                    tableHtml += '<td>' + htmlEncode(value !== null && value !== undefined ? value : '') + '</td>';
                }
            }
            tableHtml += '</tr>';
        }
        
        tableHtml += '</tbody></table></div>';
        tableHtml += '<div class="mt-2 text-white small">共 ' + data.length + ' 条记录（点击列头排序）</div>';
        $('#analysisResult').html(tableHtml);
    }

    function sortByColumn(column) {
        if (sortColumn === column) {
            sortDirection = sortDirection === 'ASC' ? 'DESC' : 'ASC';
        } else {
            sortColumn = column;
            sortDirection = 'DESC';
        }
        runDailyAnalysis();
    }

    function clearAllFilters() {
        queryConfig.forEach(function(config) {
            if (config.isVisible) {
                $('#' + config.fieldName).val('');
            }
        });
        sortColumn = '';
        sortDirection = 'DESC';
        resetStatistics();
        hideChart();
        $('#analysisResult').html('<div class="text-center text-white">请选择日期范围并输入查询条件后点击查询</div>');
    }

    function runAnalysis(type) {
        var startDate = $('#startDate').val();
        var endDate = $('#endDate').val();
        var system = $('#system').val() || '';
        
        if (!startDate || !endDate) {
            ErrorHandler.show('请选择日期范围', 'warning');
            return;
        }
        
        var url = '/daily-analysis';
        var data = {startDate: startDate, endDate: endDate, system: system};
        
        if (type === 'department') {
            url = '/department-statistics';
        } else if (type === 'doctor') {
            url = '/doctor-statistics';
            data.doctorType = 'reporter';
        } else if (type === 'category') {
            url = '/category-statistics';
        }
        
        $('#analysisResult').html('<div class="text-center text-white"><span class="spinner-border spinner-border-sm"></span> 加载中...</div>');
        
        $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function(response) {
                var data = ErrorHandler.validateResponse(response);
                if (data) {
                    displayAnalysisResult(data);
                    updateStatistics(data);
                    renderChart(data);
                }
            },
            error: ErrorHandler.handleAjaxError
        });
    }

    function updateStatistics(data) {
        if (!data || data.length === 0) {
            resetStatistics();
            return;
        }
        
        var total = 0, positive = 0, negative = 0;
        
        data.forEach(function(row) {
            var count = parseInt(row['任务数量']) || 0;
            total += count;
            positive += parseInt(row['阳性数量']) || 0;
            negative += parseInt(row['阴性数量']) || 0;
        });
        
        var rate = total > 0 ? (positive / total * 100) : 0;
        
        $('#positiveRate').text(rate.toFixed(2) + '%');
        $('#totalCount').text(total);
        $('#positiveCount').text(positive);
        $('#negativeCount').text(negative);
    }

    function resetStatistics() {
        $('#positiveRate').text('-');
        $('#totalCount').text('-');
        $('#positiveCount').text('-');
        $('#negativeCount').text('-');
    }

    function hideChart() {
        if (chartInstance) {
            chartInstance.dispose();
            chartInstance = null;
        }
        $('#chartContainer').html('<p class="text-muted text-center">图表区域预留<br>查询数据后将显示统计图表</p>');
    }

    function renderChart(data) {
        if (!data || data.length === 0) {
            hideChart();
            return;
        }
        
        var container = document.getElementById('chartContainer');
        if (!container) return;
        
        if (chartInstance) {
            chartInstance.dispose();
        }
        
        chartInstance = echarts.init(container);
        
        var categoryData = [];
        var positiveData = [];
        var negativeData = [];
        
        var topData = data.slice(0, 10);
        topData.forEach(function(row) {
            var name = row['报告医生'] || row['执行科室'] || row['检查类型'] || '未知';
            categoryData.push(name.length > 8 ? name.substring(0, 8) + '...' : name);
            positiveData.push(row['阳性数量'] || 0);
            negativeData.push(row['阴性数量'] || 0);
        });
        
        var option = {
            title: {
                text: '检查结果统计',
                left: 'center',
                textStyle: { color: '#333' }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: { type: 'shadow' }
            },
            legend: {
                data: ['阳性', '阴性'],
                bottom: 10
            },
            grid: {
                left: '3%',
                right: '4%',
                bottom: '15%',
                top: '15%',
                containLabel: true
            },
            xAxis: {
                type: 'category',
                data: categoryData,
                axisLabel: {
                    interval: 0,
                    rotate: 30,
                    fontSize: 11
                }
            },
            yAxis: {
                type: 'value',
                name: '数量'
            },
            series: [
                {
                    name: '阳性',
                    type: 'bar',
                    data: positiveData,
                    itemStyle: { color: '#ef4444' }
                },
                {
                    name: '阴性',
                    type: 'bar',
                    data: negativeData,
                    itemStyle: { color: '#22c55e' }
                }
            ]
        };
        
        chartInstance.setOption(option);
        
        window.addEventListener('resize', function() {
            if (chartInstance) {
                chartInstance.resize();
            }
        });
    }

    function setDateRange(range) {
        var today = new Date();
        var startDate, endDate;
        
        switch (range) {
            case 'today':
                startDate = endDate = today;
                break;
            case 'yesterday':
                startDate = endDate = new Date(today.getTime() - 86400000);
                break;
            case 'thisweek':
                var day = today.getDay();
                var diff = today.getDate() - day + (day === 0 ? -6 : 1);
                startDate = new Date(today.setDate(diff));
                endDate = new Date();
                break;
            case 'lastweek':
                var lastWeekStart = new Date(today.getTime() - 7 * 86400000);
                var lastDay = lastWeekStart.getDay();
                var lastDiff = lastWeekStart.getDate() - lastDay + (lastDay === 0 ? -6 : 1);
                startDate = new Date(lastWeekStart.setDate(lastDiff));
                endDate = new Date(startDate.getTime() + 6 * 86400000);
                break;
            case 'thismonth':
                startDate = new Date(today.getFullYear(), today.getMonth(), 1);
                endDate = new Date();
                break;
            case 'lastmonth':
                startDate = new Date(today.getFullYear(), today.getMonth() - 1, 1);
                endDate = new Date(today.getFullYear(), today.getMonth(), 0);
                break;
        }
        
        $('#startDate').val(startDate.toISOString().split('T')[0]);
        $('#endDate').val(endDate.toISOString().split('T')[0]);
    }

    function init() {
        history.pushState({page: 'dailyAnalysis'}, '检查每日分析', '#daily-analysis');
        $('#mainMenu').hide();
        $('#configPanel').show();
        $('#panelTitle').text('检查每日分析');
        
        var html = '<div class="row">';
        html += '<div class="col-md-1">';
        html += '<div class="bg-white rounded-3 p-2 mb-2 text-center" style="cursor: pointer;" onclick="DailyAnalysis.runAnalysis(\'department\')"><div class="fs-3 mb-1">🏥</div><h6>科室统计</h6></div>';
        html += '<div class="bg-white rounded-3 p-2 mb-2 text-center" style="cursor: pointer;" onclick="DailyAnalysis.runAnalysis(\'doctor\')"><div class="fs-3 mb-1">👨‍⚕️</div><h6>医生统计</h6></div>';
        html += '<div class="bg-white rounded-3 p-2 mb-2 text-center" style="cursor: pointer;" onclick="DailyAnalysis.runAnalysis(\'category\')"><div class="fs-3 mb-1">🔬</div><h6>检查类型</h6></div>';
        html += '<div class="bg-white rounded-3 p-2 mb-2 text-center" style="cursor: pointer;" onclick="DailyAnalysis.runAnalysis(\'result\')"><div class="fs-3 mb-1">📊</div><h6>阴阳性统计</h6></div>';
        html += '</div>';
        html += '<div class="col-md-11">';
        
        html += '<div class="mb-3"><label class="form-label text-white">日期范围</label>';
        html += '<div class="input-group" style="max-width: 400px;">';
        html += '<input type="date" class="form-control" id="startDate">';
        html += '<span class="input-group-text">至</span>';
        html += '<input type="date" class="form-control" id="endDate">';
        html += '</div>';
        html += '<div class="mt-1">';
        html += '<button class="btn btn-xs btn-outline-light" onclick="DailyAnalysis.setDateRange(\'today\')">今天</button>';
        html += '<button class="btn btn-xs btn-outline-light" onclick="DailyAnalysis.setDateRange(\'yesterday\')">昨天</button>';
        html += '<button class="btn btn-xs btn-outline-light" onclick="DailyAnalysis.setDateRange(\'thisweek\')">本周</button>';
        html += '<button class="btn btn-xs btn-outline-light" onclick="DailyAnalysis.setDateRange(\'lastweek\')">上周</button>';
        html += '<button class="btn btn-xs btn-outline-light" onclick="DailyAnalysis.setDateRange(\'thismonth\')">本月</button>';
        html += '<button class="btn btn-xs btn-outline-light" onclick="DailyAnalysis.setDateRange(\'lastmonth\')">上月</button>';
        html += '</div></div>';
        
        html += '<div id="dynamicFilters" class="d-flex flex-wrap gap-2 mb-3"></div>';
        
        html += '<div class="mb-3 d-flex gap-2">';
        html += '<button class="btn btn-primary" onclick="DailyAnalysis.runDailyAnalysis()">查询统计</button>';
        html += '<button class="btn btn-secondary" onclick="DailyAnalysis.clearAllFilters()">清除筛选</button>';
        html += '<div class="card bg-white/10 rounded-lg p-2 ms-auto d-flex align-items-center">';
        html += '<div class="d-flex gap-4">';
        html += '<div class="text-center"><div class="stat-value-sm" id="positiveRate">-</div><small>阳性率</small></div>';
        html += '<div class="text-center"><div class="stat-value-sm" id="totalCount">-</div><small>总检查数</small></div>';
        html += '<div class="text-center"><div class="stat-value-sm" id="positiveCount">-</div><small>阳性数</small></div>';
        html += '<div class="text-center"><div class="stat-value-sm" id="negativeCount">-</div><small>阴性数</small></div>';
        html += '</div></div></div>';
        
        html += '<div class="row"><div class="col-md-7">';
        html += '<div id="analysisResult" class="mt-2"><div class="text-center text-white">请选择日期范围并输入查询条件后点击查询</div></div>';
        html += '</div><div class="col-md-5">';
        html += '<div class="card bg-white rounded-lg p-3 mt-2"><h5 class="card-title text-center">统计图表</h5>';
        html += '<div id="chartContainer" style="height: 350px; display: flex; align-items: center; justify-content: center;">';
        html += '<p class="text-muted text-center">图表区域预留<br>查询数据后将显示统计图表</p>';
        html += '</div></div></div></div>';
        
        html += '</div></div>';
        
        $('#panelContent').html(html);
        
        var today = new Date();
        var firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
        $('#startDate').val(firstDay.toISOString().split('T')[0]);
        $('#endDate').val(today.toISOString().split('T')[0]);
        
        loadQueryConfig(function() {
            generateDynamicFilters();
            loadAllFieldOptions();
        });
    }

    return {
        init: init,
        runDailyAnalysis: runDailyAnalysis,
        runAnalysis: runAnalysis,
        sortByColumn: sortByColumn,
        clearAllFilters: clearAllFilters,
        setDateRange: setDateRange,
        onParentFieldChanged: onParentFieldChanged
    };
})();
