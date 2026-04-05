// Shared Authorization Module for Kaya API Explorer
// This module provides authentication configuration and helper functions
// Used by both the main API Explorer and SignalR Debug UI

let authConfig = {
  type: 'none',
  bearer: {
    token: ''
  },
  apikey: {
    name: 'X-API-Key',
    value: ''
  },
  oauth: {
    clientId: '',
    authUrl: '',
    tokenUrl: '',
    scopes: '',
    accessToken: ''
  }
}

function switchAuthType() {
  const authType = document.getElementById('authType').value
  authConfig.type = authType
  
  // Hide all auth sections
  document.querySelectorAll('.auth-section').forEach(section => {
    section.classList.add('hidden')
  })
  
  // Show the selected auth section
  switch (authType) {
    case 'none':
      document.getElementById('authNone')?.classList.remove('hidden')
      break
    case 'bearer':
      document.getElementById('authBearer')?.classList.remove('hidden')
      break
    case 'apikey':
      document.getElementById('authApiKey')?.classList.remove('hidden')
      break
    case 'oauth':
      document.getElementById('authOAuth')?.classList.remove('hidden')
      break
  }
  
  updateAuthStatus()
}

function togglePasswordVisibility(inputId, button) {
  const input = document.getElementById(inputId)
  if (!input) return
  
  const eyeOpen = button.querySelector('.eye-open')
  const eyeClosed = button.querySelector('.eye-closed')
  
  if (input.type === 'password') {
    input.type = 'text'
    if (eyeOpen) eyeOpen.style.display = 'none'
    if (eyeClosed) eyeClosed.style.display = 'block'
  } else {
    input.type = 'password'
    if (eyeOpen) eyeOpen.style.display = 'block'
    if (eyeClosed) eyeClosed.style.display = 'none'
  }
}

function saveAuthConfiguration(storageKey = 'grpcexplorer_auth', modalId = 'authModal') {
  const authType = document.getElementById('authType').value
  authConfig.type = authType
  
  switch (authType) {
    case 'none':
      authConfig.bearer.token = ''
      authConfig.apikey.value = ''
      authConfig.oauth.accessToken = ''
      break
      
    case 'bearer':
      authConfig.bearer.token = document.getElementById('authToken').value.trim()
      break
      
    case 'apikey':
      authConfig.apikey.name = document.getElementById('apiKeyName').value.trim() || 'X-API-Key'
      authConfig.apikey.value = document.getElementById('apiKeyValue').value.trim()
      break
      
    case 'oauth':
      authConfig.oauth.clientId = document.getElementById('oauthClientId').value.trim()
      authConfig.oauth.authUrl = document.getElementById('oauthAuthUrl').value.trim()
      authConfig.oauth.tokenUrl = document.getElementById('oauthTokenUrl').value.trim()
      authConfig.oauth.scopes = document.getElementById('oauthScopes').value.trim()
      authConfig.oauth.accessToken = document.getElementById('oauthAccessToken').value.trim()
      break
  }
  
  // Use TTL storage - auth expires after 24 hours for security
  KayaStorage.set(storageKey, authConfig, { ttlType: 'auth' })
  
  updateAuthStatus()
  document.getElementById(modalId).classList.remove('show')
  
  // Optional callback for logging (if addLog function exists)
  if (typeof addLog === 'function') {
    addLog('success', 'Authorization configuration saved')
  }
}

function clearAuthConfiguration(storageKey = 'grpcexplorer_auth', modalId = 'authModal') {
  authConfig = {
    type: 'none',
    bearer: { token: '' },
    apikey: { name: 'X-API-Key', value: '' },
    oauth: { clientId: '', authUrl: '', tokenUrl: '', scopes: '', accessToken: '' }
  }
  
  document.getElementById('authType').value = 'none'
  document.getElementById('authToken').value = ''
  document.getElementById('apiKeyName').value = 'X-API-Key'
  document.getElementById('apiKeyValue').value = ''
  document.getElementById('oauthClientId').value = ''
  document.getElementById('oauthAuthUrl').value = ''
  document.getElementById('oauthTokenUrl').value = ''
  document.getElementById('oauthScopes').value = ''
  document.getElementById('oauthAccessToken').value = ''
  
  KayaStorage.remove(storageKey)
  
  switchAuthType()
  updateAuthStatus()
  document.getElementById(modalId).classList.remove('show')
  
  // Optional callback for logging (if addLog function exists)
  if (typeof addLog === 'function') {
    addLog('info', 'Authorization configuration cleared')
  }
}

function loadAuthConfiguration(storageKey = 'grpcexplorer_auth') {
  try {
    const saved = KayaStorage.get(storageKey)
    if (saved) {
      authConfig = { ...authConfig, ...saved }
      
      document.getElementById('authType').value = authConfig.type
      document.getElementById('authToken').value = authConfig.bearer.token || ''
      document.getElementById('apiKeyName').value = authConfig.apikey.name || 'X-API-Key'
      document.getElementById('apiKeyValue').value = authConfig.apikey.value || ''
      document.getElementById('oauthClientId').value = authConfig.oauth.clientId || ''
      document.getElementById('oauthAuthUrl').value = authConfig.oauth.authUrl || ''
      document.getElementById('oauthTokenUrl').value = authConfig.oauth.tokenUrl || ''
      document.getElementById('oauthScopes').value = authConfig.oauth.scopes || ''
      document.getElementById('oauthAccessToken').value = authConfig.oauth.accessToken || ''
      
      switchAuthType()
      updateAuthStatus()
    }
  } catch (error) {
    console.error('Failed to load auth configuration:', error)
  }
}

function updateAuthStatus() {
  const statusDiv = document.getElementById('authStatus')
  if (!statusDiv) return
  
  const authType = authConfig.type
  
  statusDiv.classList.remove('hidden', 'success', 'info', 'warning')
  
  switch (authType) {
    case 'none':
      statusDiv.classList.add('hidden')
      break
      
    case 'bearer':
      if (authConfig.bearer.token) {
        statusDiv.classList.add('success')
        statusDiv.innerHTML = '<p>✓ JWT Bearer token is configured and will be automatically added to all requests</p>'
      } else {
        statusDiv.classList.add('hidden')
      }
      break
      
    case 'apikey':
      if (authConfig.apikey.value) {
        statusDiv.classList.add('success')
        statusDiv.innerHTML = `<p>✓ API Key (${authConfig.apikey.name}) is configured and will be automatically added to all requests</p>`
      } else {
        statusDiv.classList.add('hidden')
      }
      break
      
    case 'oauth':
      if (authConfig.oauth.accessToken) {
        statusDiv.classList.add('success')
        statusDiv.innerHTML = '<p>✓ OAuth access token is configured and will be automatically added to all requests</p>'
      } else if (authConfig.oauth.clientId && authConfig.oauth.authUrl) {
        statusDiv.classList.add('info')
        statusDiv.innerHTML = '<p>ℹ OAuth configuration saved. Use "Authorize with OAuth" to get an access token</p>'
      } else {
        statusDiv.classList.add('hidden')
      }
      break
  }
}

function getAuthHeaders() {
  const headers = {}
  
  switch (authConfig.type) {
    case 'bearer':
      if (authConfig.bearer.token) {
        headers['Authorization'] = `Bearer ${authConfig.bearer.token}`
      }
      break
    case 'apikey':
      if (authConfig.apikey.name && authConfig.apikey.value) {
        headers[authConfig.apikey.name] = authConfig.apikey.value
      }
      break
    case 'oauth':
      if (authConfig.oauth.accessToken) {
        headers['Authorization'] = `Bearer ${authConfig.oauth.accessToken}`
      }
      break
  }
  
  return headers
}

function getAccessToken() {
  switch (authConfig.type) {
    case 'bearer':
      return authConfig.bearer.token || null
    case 'oauth':
      return authConfig.oauth.accessToken || null
    default:
      return null
  }
}

function initiateOAuthFlow() {
  const clientId = document.getElementById('oauthClientId').value.trim()
  const authUrl = document.getElementById('oauthAuthUrl').value.trim()
  const scopes = document.getElementById('oauthScopes').value.trim()
  
  if (!clientId || !authUrl) {
    alert('Please enter Client ID and Authorization URL first')
    return
  }
  
  const params = new URLSearchParams({
    client_id: clientId,
    response_type: 'code',
    redirect_uri: window.location.origin + '/oauth-callback',
    scope: scopes || 'read write'
  })
  
  const oauthUrl = `${authUrl}?${params.toString()}`
  
  const popup = window.open(oauthUrl, 'oauth', 'width=500,height=600,scrollbars=yes,resizable=yes')
  
  const checkPopup = setInterval(() => {
    if (popup.closed) {
      clearInterval(checkPopup)
      alert('OAuth flow completed. Please manually enter the access token if you received one.')
    }
  }, 1000)
}

// Export auth config for external access if needed
function getAuthConfig() {
  return authConfig
}

function setAuthConfig(config) {
  authConfig = config
}
