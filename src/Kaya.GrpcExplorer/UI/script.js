// gRPC Explorer JavaScript
let services = []
let selectedService = ""
let expandedMethods = []
let methodActiveTabs = {}
let methodResponses = {}
let currentServerAddress = ""

// Active streaming sessions: methodIdentifier -> { sessionId, eventSource }
let activeStreams = {}

// Per-method message counters (persist across stream start/end until log is cleared)
let streamStats = {} // methodIdentifier -> { sent: 0, received: 0 }

// Auto-resize textarea helper
function autoResizeTextarea(el) {
  if (!el) return;
  el.style.height = "auto";
  el.style.height = el.scrollHeight + "px";
}

// Setup auto-resize for textareas in a container
function clearBodyTextarea(textareaId) {
    const el = document.getElementById(textareaId);
    if (el) {
        el.value = '';
        autoResizeTextarea(el);
    }
}

function clearBodyValues(textareaId) {
    const el = document.getElementById(textareaId);
    if (!el) return;
    try {
        const parsed = JSON.parse(el.value);
        el.value = serializeEmptied(parsed, 0);
        autoResizeTextarea(el);
    } catch {
        // not valid JSON, do nothing
    }
}

function serializeEmptied(value, indent) {
    const pad = '  '.repeat(indent);
    const innerPad = '  '.repeat(indent + 1);
    if (Array.isArray(value)) {
        return `[\n${innerPad}\n${pad}]`;
    }
    if (typeof value === 'object' && value !== null) {
        const entries = Object.entries(value);
        if (entries.length === 0) return '{}';
        const lines = entries.map(([k, v]) => {
            const val = (typeof v === 'object' && v !== null) ? serializeEmptied(v, indent + 1) : '';
            return `${innerPad}"${k}": ${val}`;
        });
        return `{\n${lines.join(',\n')}\n${pad}}`;
    }
    return '';
}

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
    const isStreaming = method.methodType === 1 || method.methodType === 2 || method.methodType === 3

    if (isStreaming) {
        return renderStreamingTryItOut(method, index, methodIdentifier)
    }

    return `
        <div class="request-builder">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                <h4>Request Body</h4>
                <div style="display: flex; gap: 6px;">
                    <button type="button" class="btn btn-outline btn-sm" onclick="clearBodyTextarea('request-${methodIdentifier}')" title="Clear body">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <polyline points="3,6 5,6 21,6"></polyline>
                            <path d="M19,6l-1,14a2,2,0,0,1-2,2H8a2,2,0,0,1-2-2L5,6"></path>
                            <path d="M10,11v6"></path><path d="M14,11v6"></path>
                            <path d="M9,6V4a1,1,0,0,1,1-1h4a1,1,0,0,1,1,1v2"></path>
                        </svg>
                        Clear
                    </button>
                    <button type="button" class="btn btn-outline btn-sm" onclick="clearBodyValues('request-${methodIdentifier}')" title="Clear example values">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="m7 21-4.3-4.3c-1-1-1-2.5 0-3.4l9.6-9.6c1-1 2.5-1 3.4 0l5.6 5.6c1 1 1 2.5 0 3.4L13 21H22"></path>
                            <path d="m5 11 9 9"></path>
                        </svg>
                        Clear values
                    </button>
                </div>
            </div>
            <textarea id="request-${methodIdentifier}" 
                      class="body-textarea" 
                      style="width: 100%; height: 200px; font-family: monospace;"
                      placeholder="Enter request JSON">${method.requestType.exampleJson}</textarea>
            
            <div class="metadata-editor">
                <h4 style="margin-bottom: 8px;">Metadata (optional)</h4>
                <div id="metadata-${methodIdentifier}"></div>
                <button class="btn btn-outline btn-sm" onclick="addMetadata('${methodIdentifier}')">Add Metadata</button>
            </div>
            
            <div style="display: flex; gap: 8px; margin-top: 16px;">
                <button class="btn btn-primary" style="flex: 1;" onclick="invokeMethod('${selectedService}', ${index})">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polygon points="5,3 19,12 5,21 5,3"></polygon>
                    </svg>
                    Invoke Method
                </button>
                <button class="btn btn-outline btn-sm" id="clear-response-btn-${methodIdentifier}"
                        style="display: none;" title="Clear response"
                        onclick="clearResponse('${methodIdentifier}')">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3,6 5,6 21,6"></polyline>
                        <path d="M19,6l-1,14a2,2,0,0,1-2,2H8a2,2,0,0,1-2-2L5,6"></path>
                        <path d="M10,11v6"></path><path d="M14,11v6"></path>
                        <path d="M9,6V4a1,1,0,0,1,1-1h4a1,1,0,0,1,1,1v2"></path>
                    </svg>
                    Clear
                </button>
            </div>
            
            <div id="response-${methodIdentifier}" style="margin-top: 16px; display: none;"></div>
        </div>
    `
}

function renderStreamingTryItOut(method, index, methodIdentifier) {
    const isServerStream = method.methodType === 1  // server only needs initial request
    const isClientOrBidi = method.methodType === 2 || method.methodType === 3

    const initialRequestArea = `
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
            <h4>${isServerStream ? 'Request Body' : 'Message Body'}</h4>
            <div style="display: flex; gap: 6px;">
                <button type="button" class="btn btn-outline btn-sm" onclick="clearBodyTextarea('request-${methodIdentifier}')" title="Clear body">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3,6 5,6 21,6"></polyline>
                        <path d="M19,6l-1,14a2,2,0,0,1-2,2H8a2,2,0,0,1-2-2L5,6"></path>
                        <path d="M10,11v6"></path><path d="M14,11v6"></path>
                        <path d="M9,6V4a1,1,0,0,1,1-1h4a1,1,0,0,1,1,1v2"></path>
                    </svg>
                    Clear
                </button>
                <button type="button" class="btn btn-outline btn-sm" onclick="clearBodyValues('request-${methodIdentifier}')" title="Clear example values">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="m7 21-4.3-4.3c-1-1-1-2.5 0-3.4l9.6-9.6c1-1 2.5-1 3.4 0l5.6 5.6c1 1 1 2.5 0 3.4L13 21H22"></path>
                        <path d="m5 11 9 9"></path>
                    </svg>
                    Clear values
                </button>
            </div>
        </div>
        <textarea id="request-${methodIdentifier}"
                  class="body-textarea"
                  style="width: 100%; font-family: monospace;"
                  placeholder="Enter JSON">${method.requestType.exampleJson}</textarea>
    `

    const metadataSection = `
        <div class="metadata-editor">
            <h4 style="margin-bottom: 8px;">Metadata (optional)</h4>
            <div id="metadata-${methodIdentifier}"></div>
            <button class="btn btn-outline btn-sm" onclick="addMetadata('${methodIdentifier}')">Add Metadata</button>
        </div>
    `

    return `
        <div class="request-builder">
            <div class="stream-status-bar">
                <span class="stream-status stream-status--idle" id="stream-status-${methodIdentifier}">Idle</span>
                <button class="btn btn-outline btn-sm stream-clear-btn" id="stream-clear-btn-${methodIdentifier}"
                        style="margin-left: auto; display: none;" title="Clear log"
                        onclick="clearStreamLog('${methodIdentifier}')">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3,6 5,6 21,6"></polyline>
                        <path d="M19,6l-1,14a2,2,0,0,1-2,2H8a2,2,0,0,1-2-2L5,6"></path>
                        <path d="M10,11v6"></path><path d="M14,11v6"></path>
                        <path d="M9,6V4a1,1,0,0,1,1-1h4a1,1,0,0,1,1,1v2"></path>
                    </svg>
                    Clear log
                </button>
            </div>

            ${initialRequestArea}
            ${metadataSection}

            <div class="stream-controls" id="stream-controls-${methodIdentifier}">
                <button class="btn btn-primary stream-start-btn" id="stream-start-btn-${methodIdentifier}"
                        onclick="startStream('${selectedService}', ${index})">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polygon points="5,3 19,12 5,21 5,3"></polygon>
                    </svg>
                    Start Stream
                </button>

                ${isClientOrBidi ? `
                <button class="btn btn-outline stream-send-btn" id="stream-send-btn-${methodIdentifier}"
                        disabled
                        onclick="sendStreamMessage('${methodIdentifier}')">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="22" y1="2" x2="11" y2="13"></line>
                        <polygon points="22,2 15,22 11,13 2,9 22,2"></polygon>
                    </svg>
                    Send Message
                </button>

                <button class="btn btn-danger stream-end-btn" id="stream-end-btn-${methodIdentifier}"
                        disabled
                        onclick="endStream('${methodIdentifier}')">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                    </svg>
                    End Stream
                </button>
                ` : `
                <button class="btn btn-danger stream-cancel-btn" id="stream-cancel-btn-${methodIdentifier}"
                        disabled
                        onclick="cancelStream('${methodIdentifier}')">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="15" y1="9" x2="9" y2="15"></line>
                        <line x1="9" y1="9" x2="15" y2="15"></line>
                    </svg>
                    Cancel
                </button>
                `}
            </div>

            <div class="stream-log" id="stream-log-${methodIdentifier}">
                <div class="stream-log-empty">Stream not started. Press <strong>Start Stream</strong> to begin.</div>
            </div>

            <div class="stream-stats" id="stream-stats-${methodIdentifier}" style="display: none;">
                <span class="stream-stats-item stream-stats-sent">
                    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                        <line x1="22" y1="2" x2="11" y2="13"></line>
                        <polygon points="22,2 15,22 11,13 2,9 22,2"></polygon>
                    </svg>
                    Sent: <strong id="stream-stats-sent-${methodIdentifier}">0</strong>
                </span>
                <span class="stream-stats-sep">&middot;</span>
                <span class="stream-stats-item stream-stats-received">
                    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                        <polyline points="17,1 21,5 17,9"></polyline>
                        <path d="M3,11V9a4,4,0,0,1,4-4h14"></path>
                        <polyline points="7,23 3,19 7,15"></polyline>
                        <path d="M21,13v2a4,4,0,0,1-4,4H3"></path>
                    </svg>
                    Received: <strong id="stream-stats-received-${methodIdentifier}">0</strong>
                </span>
            </div>
        </div>
    `
}

// --- Interactive streaming functions ------------------------------------------

async function startStream(serviceName, methodIndex) {
    const service = services.find(s => s.serviceName === serviceName)
    const method = service.methods[methodIndex]
    const methodIdentifier = `${serviceName}-${methodIndex}`

    // If a stream is already active, ignore
    if (activeStreams[methodIdentifier]) return

    const requestJson = document.getElementById(`request-${methodIdentifier}`)?.value || '{}'

    setStreamStatus(methodIdentifier, 'connecting', 'Connecting…')
    appendStreamLog(methodIdentifier, 'system', 'Starting stream…')

    try {
        const config = window.KayaGrpcExplorerConfig || {}
        const routePrefix = config.routePrefix || '/grpc-explorer'

        const authHeaders = getAuthHeaders()
        const metadata = {}
        Object.entries(authHeaders).forEach(([k, v]) => { metadata[k.toLowerCase()] = v })

        // Collect custom metadata rows
        const metadataContainer = document.getElementById(`metadata-${methodIdentifier}`)
        if (metadataContainer) {
            metadataContainer.querySelectorAll('.metadata-row').forEach(row => {
                const inputs = row.querySelectorAll('.metadata-input')
                if (inputs.length >= 2 && inputs[0].value) {
                    metadata[inputs[0].value.toLowerCase()] = inputs[1].value
                }
            })
        }

        const body = {
            serverAddress: currentServerAddress,
            serviceName,
            methodName: method.methodName,
            metadata,
            initialMessageJson: requestJson  // used for server-streaming; ignored otherwise
        }

        const res = await fetch(`${routePrefix}/stream/start`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        })

        if (!res.ok) {
            const err = await res.json()
            setStreamStatus(methodIdentifier, 'error', 'Error')
            appendStreamLog(methodIdentifier, 'error', `Failed to start: ${err.error}`)
            return
        }

        const { sessionId } = await res.json()

        // Open SSE connection
        const evtSource = new EventSource(`${routePrefix}/stream/events/${sessionId}`)

        evtSource.addEventListener('message', e => {
            let pretty
            try { pretty = JSON.stringify(JSON.parse(e.data), null, 2) } catch { pretty = e.data }
            appendStreamLog(methodIdentifier, 'received', pretty)
        })

        evtSource.addEventListener('complete', () => {
            setStreamStatus(methodIdentifier, 'complete', 'Complete')
            appendStreamLog(methodIdentifier, 'system', 'Stream complete.')
            evtSource.close()
            delete activeStreams[methodIdentifier]
            setStreamButtonsIdle(methodIdentifier, method.methodType)
        })

        evtSource.addEventListener('error', e => {
            const msg = e.data || 'Unknown error'
            setStreamStatus(methodIdentifier, 'error', 'Error')
            appendStreamLog(methodIdentifier, 'error', `Error: ${msg}`)
            evtSource.close()
            delete activeStreams[methodIdentifier]
            setStreamButtonsIdle(methodIdentifier, method.methodType)
        })

        evtSource.onerror = () => {
            if (evtSource.readyState === EventSource.CLOSED) {
                if (activeStreams[methodIdentifier]) {
                    setStreamStatus(methodIdentifier, 'error', 'Disconnected')
                    appendStreamLog(methodIdentifier, 'error', 'SSE connection closed unexpectedly.')
                    delete activeStreams[methodIdentifier]
                    setStreamButtonsIdle(methodIdentifier, method.methodType)
                }
            }
        }

        activeStreams[methodIdentifier] = { sessionId, eventSource: evtSource }
        setStreamStatus(methodIdentifier, 'streaming', 'Streaming')
        setStreamButtonsActive(methodIdentifier, method.methodType)

        // For server streaming, log the sent request
        if (method.methodType === 1) {
            appendStreamLog(methodIdentifier, 'sent', requestJson)
        }

    } catch (err) {
        setStreamStatus(methodIdentifier, 'error', 'Error')
        appendStreamLog(methodIdentifier, 'error', `Failed: ${err.message}`)
    }
}

async function sendStreamMessage(methodIdentifier) {
    const stream = activeStreams[methodIdentifier]
    if (!stream) return

    const textarea = document.getElementById(`request-${methodIdentifier}`)
    const messageJson = textarea?.value || '{}'

    try {
        const config = window.KayaGrpcExplorerConfig || {}
        const routePrefix = config.routePrefix || '/grpc-explorer'

        const res = await fetch(`${routePrefix}/stream/send`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sessionId: stream.sessionId, messageJson })
        })

        if (!res.ok) {
            const err = await res.json()
            appendStreamLog(methodIdentifier, 'error', `Send failed: ${err.error}`)
            return
        }

        appendStreamLog(methodIdentifier, 'sent', messageJson)
    } catch (err) {
        appendStreamLog(methodIdentifier, 'error', `Send failed: ${err.message}`)
    }
}

async function endStream(methodIdentifier) {
    const stream = activeStreams[methodIdentifier]
    if (!stream) return

    try {
        const config = window.KayaGrpcExplorerConfig || {}
        const routePrefix = config.routePrefix || '/grpc-explorer'

        appendStreamLog(methodIdentifier, 'system', 'Ending stream…')

        await fetch(`${routePrefix}/stream/end`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sessionId: stream.sessionId })
        })
    } catch (err) {
        appendStreamLog(methodIdentifier, 'error', `End stream failed: ${err.message}`)
    }
}

function cancelStream(methodIdentifier) {
    const stream = activeStreams[methodIdentifier]
    if (!stream) return

    stream.eventSource.close()
    delete activeStreams[methodIdentifier]
    setStreamStatus(methodIdentifier, 'idle', 'Idle')
    appendStreamLog(methodIdentifier, 'system', 'Stream cancelled.')
    const cancelBtn = document.getElementById(`stream-cancel-btn-${methodIdentifier}`)
    if (cancelBtn) setStreamButtonsIdle(methodIdentifier, 1) // server-stream type
}

// --- Stream UI helpers --------------------------------------------------------

function setStreamStatus(methodIdentifier, state, label) {
    const el = document.getElementById(`stream-status-${methodIdentifier}`)
    if (!el) return
    el.className = `stream-status stream-status--${state}`
    el.textContent = label
}

function setStreamButtonsActive(methodIdentifier, methodType) {
    const startBtn = document.getElementById(`stream-start-btn-${methodIdentifier}`)
    if (startBtn) startBtn.disabled = true

    if (methodType === 2 || methodType === 3) {
        const sendBtn = document.getElementById(`stream-send-btn-${methodIdentifier}`)
        const endBtn = document.getElementById(`stream-end-btn-${methodIdentifier}`)
        if (sendBtn) sendBtn.disabled = false
        if (endBtn) endBtn.disabled = false
    } else {
        const cancelBtn = document.getElementById(`stream-cancel-btn-${methodIdentifier}`)
        if (cancelBtn) cancelBtn.disabled = false
    }
}

function setStreamButtonsIdle(methodIdentifier, methodType) {
    const startBtn = document.getElementById(`stream-start-btn-${methodIdentifier}`)
    if (startBtn) startBtn.disabled = false

    if (methodType === 2 || methodType === 3) {
        const sendBtn = document.getElementById(`stream-send-btn-${methodIdentifier}`)
        const endBtn = document.getElementById(`stream-end-btn-${methodIdentifier}`)
        if (sendBtn) sendBtn.disabled = true
        if (endBtn) endBtn.disabled = true
    } else {
        const cancelBtn = document.getElementById(`stream-cancel-btn-${methodIdentifier}`)
        if (cancelBtn) cancelBtn.disabled = true
    }
}

function appendStreamLog(methodIdentifier, type, text) {
    const log = document.getElementById(`stream-log-${methodIdentifier}`)
    if (!log) return

    // Remove empty placeholder
    const empty = log.querySelector('.stream-log-empty')
    if (empty) empty.remove()

    // Show the clear button once there's content
    const clearBtn = document.getElementById(`stream-clear-btn-${methodIdentifier}`)
    if (clearBtn) clearBtn.style.display = ''

    // Track sent/received counts and update the stats bar
    if (type === 'sent' || type === 'received') {
        if (!streamStats[methodIdentifier]) streamStats[methodIdentifier] = { sent: 0, received: 0 }
        streamStats[methodIdentifier][type]++
        updateStreamStats(methodIdentifier)
    }

    const now = new Date().toLocaleTimeString('en-GB', { hour12: false })

    const icon  = { sent: '\u2191', received: '\u2193', system: '\u00b7', error: '\u2717' }[type] || '\u00b7'
    const label = { sent: 'SENT', received: 'RECV', system: 'INFO', error: 'ERR'  }[type] || 'INFO'
    const copyable = type === 'sent' || type === 'received'

    const entry = document.createElement('div')
    entry.className = `stream-log-entry stream-log-entry--${type}`

    entry.innerHTML = `
        <span class="stream-log-meta">
            <span class="stream-log-icon">${icon}</span>
            <span class="stream-log-label">${label}</span>
            <span class="stream-log-time">${now}</span>
            ${copyable ? `
            <button class="stream-log-copy-btn" title="Copy" onclick="copyStreamEntry(this)">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                </svg>
            </button>` : ''}
        </span>
        <pre class="stream-log-body">${escapeHtml(text)}</pre>
    `

    log.appendChild(entry)
    log.scrollTop = log.scrollHeight
}

function updateStreamStats(methodIdentifier) {
    const stats = streamStats[methodIdentifier] || { sent: 0, received: 0 }
    const bar = document.getElementById(`stream-stats-${methodIdentifier}`)
    const sentEl = document.getElementById(`stream-stats-sent-${methodIdentifier}`)
    const receivedEl = document.getElementById(`stream-stats-received-${methodIdentifier}`)
    if (bar) bar.style.display = ''
    if (sentEl) sentEl.textContent = stats.sent
    if (receivedEl) receivedEl.textContent = stats.received
}

function copyStreamEntry(btn) {
    const pre = btn.closest('.stream-log-entry').querySelector('.stream-log-body')
    if (!pre) return
    navigator.clipboard.writeText(pre.textContent).then(() => {
        const original = btn.innerHTML
        btn.innerHTML = '\u2713'
        setTimeout(() => { btn.innerHTML = original }, 1500)
    })
}

function clearStreamLog(methodIdentifier) {
    const log = document.getElementById(`stream-log-${methodIdentifier}`)
    if (!log) return
    log.innerHTML = '<div class="stream-log-empty">Log cleared. Press <strong>Start Stream</strong> to begin a new session.</div>'
    const clearBtn = document.getElementById(`stream-clear-btn-${methodIdentifier}`)
    if (clearBtn) clearBtn.style.display = 'none'
    // Reset stats
    delete streamStats[methodIdentifier]
    const bar = document.getElementById(`stream-stats-${methodIdentifier}`)
    if (bar) bar.style.display = 'none'
    const sentEl = document.getElementById(`stream-stats-sent-${methodIdentifier}`)
    const receivedEl = document.getElementById(`stream-stats-received-${methodIdentifier}`)
    if (sentEl) sentEl.textContent = '0'
    if (receivedEl) receivedEl.textContent = '0'
}

function showClearResponseBtn(methodIdentifier) {
    const btn = document.getElementById(`clear-response-btn-${methodIdentifier}`)
    if (btn) btn.style.display = ''
}

function clearResponse(methodIdentifier) {
    const container = document.getElementById(`response-${methodIdentifier}`)
    if (container) {
        container.innerHTML = ''
        container.style.display = 'none'
    }
    const clearBtn = document.getElementById(`clear-response-btn-${methodIdentifier}`)
    if (clearBtn) clearBtn.style.display = 'none'
    delete methodResponses[methodIdentifier]
}

function escapeHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
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
            const responseJson = result.responseJson
            const responseSize = new Blob([responseJson]).size
            
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
            showClearResponseBtn(methodIdentifier);
            
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
            showClearResponseBtn(methodIdentifier);
            
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
        showClearResponseBtn(methodIdentifier);
        
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
