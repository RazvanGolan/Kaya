// gRPC Explorer JavaScript
let services = []
let selectedService = ""
let expandedMethods = []
let methodActiveTabs = {}
let methodResponses = {}
let currentServerAddress = ""

// Auto-resize textarea helper
function autoResizeTextarea(el) {
  if (!el) return;
  el.style.height = "auto";
  el.style.height = el.scrollHeight + "px";
}

// Setup auto-resize for textareas in a container
function setupTextareaAutoResize(container) {
  const textareas = container ? container.querySelectorAll('.body-textarea, .auth-textarea, textarea') : document.querySelectorAll('.body-textarea, .auth-textarea, textarea');
  textareas.forEach(textarea => {
    // Remove existing listener to avoid duplicates
    textarea.removeEventListener('input', textarea._autoResizeHandler);
    // Create and store the handler
    textarea._autoResizeHandler = () => autoResizeTextarea(textarea);
    textarea.addEventListener('input', textarea._autoResizeHandler);
    // Initial resize for prefilled content
    autoResizeTextarea(textarea);
  });
}

// Theme Management
function initializeTheme() {
    const config = window.KayaGrpcExplorerConfig || { defaultTheme: 'light' }
    const savedTheme = localStorage.getItem('kayaGrpcTheme') || config.defaultTheme
    document.documentElement.setAttribute('data-theme', savedTheme)
    updateThemeIcons()
}

function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-theme')
    const newTheme = currentTheme === 'light' ? 'dark' : 'light'
    document.documentElement.setAttribute('data-theme', newTheme)
    localStorage.setItem('kayaGrpcTheme', newTheme)
    updateThemeIcons()
}

function updateThemeIcons() {
    const currentTheme = document.documentElement.getAttribute('data-theme')
    const sunIcon = document.querySelector('.sun-icon')
    const moonIcon = document.querySelector('.moon-icon')
    const themeText = document.querySelector('.theme-text')
    
    if (currentTheme === 'dark') {
        if (sunIcon) sunIcon.style.display = 'block'
        if (moonIcon) moonIcon.style.display = 'none'
        if (themeText) themeText.textContent = 'Light'
    } else {
        if (sunIcon) sunIcon.style.display = 'none'
        if (moonIcon) moonIcon.style.display = 'block'
        if (themeText) themeText.textContent = 'Dark'
    }
}

// Modal Management
function showModal(modalId) {
    const modal = document.getElementById(modalId)
    if (modal) {
        modal.classList.add('show')
        // Auto-resize textareas in the modal
        setupTextareaAutoResize(modal);
    }
}

function hideModal(modalId) {
    const modal = document.getElementById(modalId)
    if (modal) {
        modal.classList.remove('show')
    }
}

function initializeModals() {
    // Close modals when clicking outside
    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                hideModal(modal.id)
            }
        })
    })
}

// Initialize the application
document.addEventListener("DOMContentLoaded", async () => {
    initializeTheme()
    loadAuthConfiguration('kayaGrpcAuthConfig')
    
    // Get config
    const config = window.KayaGrpcExplorerConfig || {}
    currentServerAddress = sessionStorage.getItem('grpcServerAddress') || config.defaultServerAddress || window.location.host
    
    document.getElementById('serverAddress').value = currentServerAddress
    
    // Load services
    await loadServices()
    
    // Event listeners
    document.getElementById('searchInput').addEventListener('input', filterServices)
    document.getElementById('themeToggleBtn').addEventListener('click', toggleTheme)
    document.getElementById('serverConfigBtn').addEventListener('click', () => showModal('serverModal'))
    document.getElementById('authorizeBtn').addEventListener('click', () => showModal('authModal'))
    
    document.getElementById('closeServerModal').addEventListener('click', () => hideModal('serverModal'))
    document.getElementById('closeAuth').addEventListener('click', () => hideModal('authModal'))
    
    document.getElementById('saveServerBtn').addEventListener('click', saveServerConfig)
    document.getElementById('saveAuthBtn').addEventListener('click', () => saveAuthConfiguration('kayaGrpcAuthConfig', 'authModal'))
    document.getElementById('clearAuthBtn').addEventListener('click', () => clearAuthConfiguration('kayaGrpcAuthConfig', 'authModal'))
    
    initializeModals()
})

async function loadServices() {
    try {
        const config = window.KayaGrpcExplorerConfig || {}
        const routePrefix = config.routePrefix || '/grpc-explorer'
        
        const response = await fetch(`${routePrefix}/services?serverAddress=${encodeURIComponent(currentServerAddress)}`)
        
        if (!response.ok) {
            const error = await response.json()
            showError(`Failed to load services: ${error.error || response.statusText}`)
            services = []
            renderServices()
            return
        }
        
        services = await response.json()
        
        if (services.length > 0) {
            selectedService = services[0].serviceName
        }
        
        renderServices()
        renderMethods()
    } catch (error) {
        console.error('Failed to load services:', error)
        showError(`Failed to connect to ${currentServerAddress}. Ensure gRPC reflection is enabled.`)
        services = []
        renderServices()
    }
}

function saveServerConfig() {
    currentServerAddress = document.getElementById('serverAddress').value.trim()
    sessionStorage.setItem('grpcServerAddress', currentServerAddress)
    hideModal('serverModal')
    
    // Reload services with new address
    services = []
    selectedService = ""
    expandedMethods = []
    loadServices()
}

function showError(message) {
    const methodsList = document.getElementById('methodsList')
    methodsList.innerHTML = `
        <div class="server-status disconnected">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="15" y1="9" x2="9" y2="15"></line>
                <line x1="9" y1="9" x2="15" y2="15"></line>
            </svg>
            ${message}
        </div>
    `
}

function renderServices() {
    const container = document.getElementById('servicesList')
    container.innerHTML = ''
    
    if (services.length === 0) {
        container.innerHTML = '<p class="text-muted" style="padding: 16px; text-align: center;">No services found</p>'
        return
    }
    
    const query = document.getElementById('searchInput').value.toLowerCase().trim()
    
    services.forEach(service => {
        const card = document.createElement('div')
        card.className = `service-card ${selectedService === service.serviceName ? 'active' : ''}`
        card.onclick = () => selectService(service.serviceName)
        
        const methodTypeCounts = {}
        service.methods.forEach(method => {
            const typeName = getMethodTypeBadgeClass(method.methodType)
            methodTypeCounts[typeName] = (methodTypeCounts[typeName] || 0) + 1
        })
        
        const badges = Object.entries(methodTypeCounts)
            .map(([type, count]) => `<span class="badge ${type}">${getMethodTypeDisplay(type)} (${count})</span>`)
            .join('')
        
        card.innerHTML = `
            <h3>${service.simpleName}</h3>
            <p class="service-package">${service.package || 'default'}</p>
            <div class="method-type-badges">${badges}</div>
        `
        
        if (query && !shouldServiceBeVisible(service, query)) {
            card.style.display = 'none'
        }
        
        container.appendChild(card)
    })
}

function renderMethods() {
    const service = services.find(s => s.serviceName === selectedService)
    if (!service) {
        document.getElementById('methodsList').innerHTML = '<p class="text-muted" style="text-align: center;">Select a service</p>'
        return
    }
    
    document.getElementById('serviceTitle').textContent = service.simpleName
    document.getElementById('serviceDescription').textContent = service.description || `Package: ${service.package || 'default'}`
    
    const container = document.getElementById('methodsList')
    
    saveMethodStates()
    
    container.innerHTML = ''
    
    const query = document.getElementById('searchInput').value.toLowerCase().trim()
    
    let methodsToShow = service.methods
    if (query) {
        methodsToShow = service.methods.filter(method => 
            doesMethodMatchQuery(method, query)
        )
    }
    
    methodsToShow.forEach((method, index) => {
        const originalIndex = service.methods.indexOf(method)
        const methodId = `${selectedService}-${originalIndex}`
        const isExpanded = expandedMethods.includes(methodId)
        
        const card = document.createElement('div')
        card.className = 'method-card'
        
        const typeBadge = getMethodTypeBadgeClass(method.methodType)
        
        card.innerHTML = `
            <div class="method-header" onclick="toggleMethod('${methodId}')">
                <div class="method-title">
                    <div class="method-name-type">
                        <span class="badge ${typeBadge}">${getMethodTypeDisplay(typeBadge)}</span>
                        <code class="method-name">${method.methodName}</code>
                    </div>
                    <svg class="chevron ${isExpanded ? 'expanded' : ''}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="9,18 15,12 9,6"></polyline>
                    </svg>
                </div>
                <div class="method-info">
                    <h4>${method.methodName}</h4>
                    <p>${method.description || 'No description available'}</p>
                </div>
            </div>
            <div class="method-content ${isExpanded ? 'expanded' : ''}" id="content-${methodId}">
                ${renderMethodTabs(method, methodId, originalIndex)}
            </div>
        `
        
        container.appendChild(card)
    })
    
    if (query && methodsToShow.length === 0) {
        container.innerHTML = '<p class="text-muted" style="text-align: center; padding: 2rem;">No methods match your search query.</p>'
    }
    
    restoreMethodStates()
}

function renderMethodTabs(method, methodId, index) {
    const activeTab = methodActiveTabs[methodId] || 'request';
    
    return `
        <div class="tabs">
            <div class="tab-list">
                <button class="tab-trigger ${activeTab === 'request' ? 'active' : ''}" onclick="switchTab(event, '${methodId}', 'request')">Request</button>
                <button class="tab-trigger ${activeTab === 'response' ? 'active' : ''}" onclick="switchTab(event, '${methodId}', 'response')">Response</button>
                <button class="tab-trigger ${activeTab === 'try' ? 'active' : ''}" onclick="switchTab(event, '${methodId}', 'try')">Try it out</button>
            </div>
            
            <div class="tab-content ${activeTab === 'request' ? 'active' : ''}" id="${methodId}-request">
                ${renderMessageSchema(method.requestType, 'Request')}
            </div>
            
            <div class="tab-content ${activeTab === 'response' ? 'active' : ''}" id="${methodId}-response">
                ${renderMessageSchema(method.responseType, 'Response')}
            </div>
            
            <div class="tab-content ${activeTab === 'try' ? 'active' : ''}" id="${methodId}-try">
                ${renderTryItOut(method, index)}
            </div>
        </div>
    `
}

function renderMessageSchema(schema, label) {
    return `
        <div class="message-schema">
            <h5>${schema.typeName}</h5>
            ${schema.description ? `<p style="font-size: 13px; color: var(--text-secondary); margin-bottom: 8px;">${schema.description}</p>` : ''}
            <div class="fields-list">
                ${schema.fields.map(field => `
                    <div class="field-item">
                        <div class="field-header">
                            <span class="field-name">${field.name}</span>
                            <span class="field-type">${field.type}${field.isRepeated ? '[]' : ''}</span>
                            ${field.isOptional ? '<span class="badge" style="font-size: 10px;">optional</span>' : ''}
                        </div>
                        ${field.description ? `<div class="field-description">${field.description}</div>` : ''}
                    </div>
                `).join('')}
            </div>
            <div style="margin-top: 12px;">
                <h6 style="font-size: 12px; font-weight: 600; margin-bottom: 4px;">Example JSON:</h6>
                <div class="code-block">
                    <pre><code>${schema.exampleJson}</code></pre>
                </div>
            </div>
        </div>
    `
}

function renderTryItOut(method, index) {
    const methodIdentifier = `${selectedService}-${index}`
    
    return `
        <div class="request-builder">
            <h4 style="margin-bottom: 12px;">Request Body</h4>
            <textarea id="request-${methodIdentifier}" 
                      class="body-textarea" 
                      style="width: 100%; height: 200px; font-family: monospace;"
                      placeholder="Enter request JSON">${method.requestType.exampleJson}</textarea>
            
            <div class="metadata-editor">
                <h4 style="margin-bottom: 8px;">Metadata (optional)</h4>
                <div id="metadata-${methodIdentifier}"></div>
                <button class="btn btn-outline btn-sm" onclick="addMetadata('${methodIdentifier}')">Add Metadata</button>
            </div>
            
            <button class="btn btn-primary" style="margin-top: 16px; width: 100%;" onclick="invokeMethod('${selectedService}', ${index})">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polygon points="5,3 19,12 5,21 5,3"></polygon>
                </svg>
                Invoke Method
            </button>
            
            <div id="response-${methodIdentifier}" style="margin-top: 16px; display: none;"></div>
        </div>
    `
}

function selectService(serviceName) {
    selectedService = serviceName
    expandedMethods = []
    methodActiveTabs = {}
    methodResponses = {}
    renderServices()
    renderMethods()
}

function toggleMethod(methodId) {
    const index = expandedMethods.indexOf(methodId)
    if (index > -1) {
        expandedMethods.splice(index, 1)
    } else {
        expandedMethods.push(methodId)
    }

    // Directly toggle the DOM without re-rendering, so form inputs are preserved
    const content = document.getElementById(`content-${methodId}`)
    if (!content) return

    const isExpanded = expandedMethods.includes(methodId)
    content.classList.toggle('expanded', isExpanded)

    const chevron = content.previousElementSibling?.querySelector('.chevron')
    if (chevron) chevron.classList.toggle('expanded', isExpanded)
}

function switchTab(event, methodId, tabName) {
    const tabList = event.target.parentElement
    tabList.querySelectorAll('.tab-trigger').forEach(trigger => {
        trigger.classList.remove('active')
    })
    event.target.classList.add('active')
    
    const tabsContainer = tabList.parentElement
    tabsContainer.querySelectorAll('.tab-content').forEach(content => {
        content.classList.remove('active')
    })
    
    document.getElementById(`${methodId}-${tabName}`).classList.add('active')
    
    methodActiveTabs[methodId] = tabName;
    
    const activeTab = document.getElementById(`${methodId}-${tabName}`);
    if (activeTab) setupTextareaAutoResize(activeTab);
}

async function invokeMethod(serviceName, methodIndex) {
    const service = services.find(s => s.serviceName === serviceName)
    const method = service.methods[methodIndex]
    const methodIdentifier = `${serviceName}-${methodIndex}`
    
    const requestJson = document.getElementById(`request-${methodIdentifier}`).value
    const responseContainer = document.getElementById(`response-${methodIdentifier}`)
    
    responseContainer.style.display = 'block'
    responseContainer.innerHTML = '<p>Invoking method...</p>'
    
    // Store the invoking state
    methodResponses[methodIdentifier] = {
        html: '<p>Invoking method...</p>',
        visible: true
    };
    
    try {
        const config = window.KayaGrpcExplorerConfig || {}
        const routePrefix = config.routePrefix || '/grpc-explorer'
        
        const authHeaders = getAuthHeaders()
        const metadata = {}
        
        // Convert auth headers to metadata
        Object.entries(authHeaders).forEach(([key, value]) => {
            metadata[key.toLowerCase()] = value
        })
        
        const requestBody = {
            serverAddress: currentServerAddress,
            serviceName: serviceName,
            methodName: method.methodName,
            requestJson: requestJson,
            metadata: metadata
        }
        
        const requestBodyStr = JSON.stringify(requestBody)
        const requestSize = new Blob([requestBodyStr]).size
        
        const response = await fetch(`${routePrefix}/invoke`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: requestBodyStr
        })
        
        const result = await response.json()
        
        if (result.success) {
            let responseJson
            let responseSize
            
            // Handle streaming vs unary responses
            if (result.streamResponses && result.streamResponses.length > 0) {
                // For streaming responses, format each JSON string in the array
                const formattedResponses = result.streamResponses.map((jsonStr, index) => {
                    try {
                        const parsed = JSON.parse(jsonStr)
                        return JSON.stringify(parsed, null, 2).trim()
                    } catch (e) {
                        return jsonStr
                    }
                })
                // Join all responses without extra spacing
                responseJson = formattedResponses.join(',\n')
                responseSize = new Blob([responseJson]).size
            } else {
                // For unary responses, use the responseJson directly
                responseJson = result.responseJson
                responseSize = new Blob([responseJson]).size
            }
            
            const duration = result.durationMs || 0
            
            const successHtml = `
                ${generatePerformanceHtml(duration, requestSize, responseSize)}
                <div class="code-block" style="position: relative;">
                    <div style="position: absolute; top: 8px; right: 8px; z-index: 1; display: flex; gap: 4px;">
                        <button class="copy-btn" onclick="copyResponseToClipboard(this)" title="Copy to clipboard">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                            </svg>
                        </button>
                        <button class="copy-btn save-btn" 
                                onclick="saveResponseToFile(this, '${method.methodName}')" 
                                title="Save to file">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
                                <polyline points="17,21 17,13 7,13 7,21"></polyline>
                                <polyline points="7,3 7,8 15,8"></polyline>
                            </svg>
                        </button>
                    </div>
                    <pre>${responseJson}</pre>
                </div>
            `;
            
            responseContainer.innerHTML = successHtml;
            
            // Store the successful response
            methodResponses[methodIdentifier] = {
                html: successHtml,
                visible: true
            };
        } else {
            const errorHtml = `
                <div class="server-status disconnected">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="15" y1="9" x2="9" y2="15"></line>
                        <line x1="9" y1="9" x2="15" y2="15"></line>
                    </svg>
                    Error: ${result.errorMessage}
                </div>
            `;
            
            responseContainer.innerHTML = errorHtml;
            
            // Store the error response
            methodResponses[methodIdentifier] = {
                html: errorHtml,
                visible: true
            };
        }
    } catch (error) {
        const errorHtml = `
            <div class="server-status disconnected">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="15" y1="9" x2="9" y2="15"></line>
                    <line x1="9" y1="9" x2="15" y2="15"></line>
                </svg>
                Request failed: ${error.message}
            </div>
        `;
        
        responseContainer.innerHTML = errorHtml;
        
        // Store the error response
        methodResponses[methodIdentifier] = {
            html: errorHtml,
            visible: true
        };
    }
}

function addMetadata(methodIdentifier) {
    const container = document.getElementById(`metadata-${methodIdentifier}`)
    const row = document.createElement('div')
    row.className = 'metadata-row'
    row.innerHTML = `
        <input type="text" placeholder="Key" class="metadata-input" style="flex: 1;">
        <input type="text" placeholder="Value" class="metadata-input" style="flex: 2;">
        <button class="btn btn-outline btn-sm" onclick="this.parentElement.remove()">&times;</button>
    `
    container.appendChild(row)
}

function getMethodTypeBadgeClass(methodType) {
    switch (methodType) {
        case 0: return 'unary'
        case 1: return 'server-stream'
        case 2: return 'client-stream'
        case 3: return 'bidi-stream'
        default: return 'unary'
    }
}

function getMethodTypeDisplay(badgeClass) {
    switch (badgeClass) {
        case 'unary': return 'Unary'
        case 'server-stream': return 'Server Stream'
        case 'client-stream': return 'Client Stream'
        case 'bidi-stream': return 'Bidi Stream'
        default: return 'Unary'
    }
}

function doesMethodMatchQuery(method, query) {
    if (!query || !query.trim()) return true
    
    const lowerQuery = query.toLowerCase()
    const methodName = method.methodName.toLowerCase()
    const methodDescription = (method.description || '').toLowerCase()
    
    return methodName.includes(lowerQuery) || methodDescription.includes(lowerQuery)
}

function shouldServiceBeVisible(service, query) {
    if (!query || !query.trim()) return true
    
    return service.methods.some(method => doesMethodMatchQuery(method, query))
}

function filterServices() {
    const query = document.getElementById('searchInput').value.toLowerCase().trim()
    const clearBtn = document.getElementById('clearSearchBtn')
    
    if (query) {
        clearBtn.style.display = 'flex'
    } else {
        clearBtn.style.display = 'none'
    }
    
    const visibleServices = query ? services.filter(s => shouldServiceBeVisible(s, query)) : services
    
    if (query) {
        if (visibleServices.length === 1) {
            if (selectedService !== visibleServices[0].serviceName) {
                selectedService = visibleServices[0].serviceName
                expandedMethods = []
            }
        } else if (visibleServices.length > 1) {
            const isCurrentSelectionVisible = visibleServices.some(s => s.serviceName === selectedService)
            if (!isCurrentSelectionVisible) {
                selectedService = visibleServices[0].serviceName
                expandedMethods = []
            }
        }
        renderServices()
    } else {
        if (!selectedService && services.length > 0) {
            selectedService = services[0].serviceName
            expandedMethods = []
        }
        renderServices()
    }
    
    renderMethods()
}

function clearSearch() {
    document.getElementById('searchInput').value = ''
    document.getElementById('clearSearchBtn').style.display = 'none'
    filterServices()
}

// Helper functions to save and restore method states
function saveMethodStates() {
    document.querySelectorAll('.method-card').forEach(card => {
        const tabList = card.querySelector('.tab-list');
        if (tabList) {
            const activeTabButton = tabList.querySelector('.tab-trigger.active');
            if (activeTabButton) {
                const onClickAttr = activeTabButton.getAttribute('onclick');
                const match = onClickAttr.match(/switchTab\(event, '([^']+)', '([^']+)'\)/);
                if (match) {
                    const methodId = match[1];
                    const tabName = match[2];
                    methodActiveTabs[methodId] = tabName;
                }
            }
        }
    });
    
    Object.keys(methodResponses).forEach(methodId => {
        const responseContainer = document.getElementById(`response-${methodId}`);
        if (responseContainer) {
            methodResponses[methodId] = {
                html: responseContainer.innerHTML,
                visible: responseContainer.style.display !== 'none'
            };
        }
    });
}

function restoreMethodStates() {
    Object.keys(methodResponses).forEach(methodId => {
        const responseContainer = document.getElementById(`response-${methodId}`);
        if (responseContainer && methodResponses[methodId]) {
            responseContainer.innerHTML = methodResponses[methodId].html;
            responseContainer.style.display = methodResponses[methodId].visible ? 'block' : 'none';
        }
    });
    
    setupTextareaAutoResize();
}

function formatBytes(bytes) {
    if (bytes === 0) return '0 B'
    const k = 1024
    const sizes = ['B', 'KB', 'MB', 'GB']
    const i = Math.floor(Math.log(bytes) / Math.log(k))
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

function formatDuration(ms) {
    if (ms < 1000) return `${ms}ms`
    return `${(ms / 1000).toFixed(1)}s`
}

function getDurationColor(ms) {
    if (ms < 500) return '#22c55e' 
    if (ms < 1000) return '#f59e0b'
    return '#ef4444'
}

function getSizeColor(bytes) {
    if (bytes < 1024) return '#22c55e'
    if (bytes < 1024 * 100) return '#f59e0b'
    return '#ef4444'
}

function generatePerformanceHtml(duration, requestSize, responseSize) {
    return `
        <div class="performance-metrics" style="background: var(--bg-secondary); border-radius: 6px; padding: 12px; margin-bottom: 12px; border-left: 4px solid ${getDurationColor(duration)};">
            <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 8px;">
                <h6 style="margin: 0; color: var(--text-primary);">Performance</h6>
            </div>
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 8px; font-size: 0.85em;">
                <div><strong>Duration:</strong> <span style="color: ${getDurationColor(duration)}; font-weight: 600;">${formatDuration(duration)}</span></div>
                <div><strong>Request:</strong> <span style="color: ${getSizeColor(requestSize)}; font-weight: 600;">${formatBytes(requestSize)}</span></div>
                <div><strong>Response:</strong> <span style="color: ${getSizeColor(responseSize)}; font-weight: 600;">${formatBytes(responseSize)}</span></div>
            </div>
        </div>
    `
}

function copyResponseToClipboard(button) {
    const buttonContainer = button.parentElement
    const preElement = buttonContainer.nextElementSibling
    const responseBody = preElement ? preElement.textContent : ''
    
    navigator.clipboard.writeText(responseBody).then(() => {
        const originalIcon = button.innerHTML
        button.innerHTML = "✓"
        setTimeout(() => {
            button.innerHTML = originalIcon
        }, 2000)
    })
}

function saveResponseToFile(button, methodName) {
    const buttonContainer = button.parentElement
    const preElement = buttonContainer.nextElementSibling
    const content = preElement ? preElement.textContent : ''
    
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-')
    const cleanMethodName = methodName.replace(/[^a-zA-Z0-9]/g, '')
    const filename = `grpc-${cleanMethodName}-response-${timestamp}.json`
    
    const blob = new Blob([content], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    a.click()
    URL.revokeObjectURL(url)
    
    const originalIcon = button.innerHTML
    button.innerHTML = "✓"
    setTimeout(() => {
        button.innerHTML = originalIcon
    }, 2000)
}
