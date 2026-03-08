let controllers = []
let selectedController = ""
let expandedEndpoints = []
let endpointActiveTabs = {}
let endpointResponses = {}

const requestHeaders = [{ key: "Content-Type", value: "application/json" }]

let currentTheme = getInitialTheme()
let logoClickCount = 0
let logoClickTimer = null

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

function generateCurlCode(url, method, headers, body, formFields = null) {
  let curlCommand = `curl -X ${method.toUpperCase()} "${url}"`;
  
  // Only include Content-Type header if it's not multipart/form-data (curl sets it automatically with -F)
  Object.entries(headers).forEach(([key, value]) => {
    if (!(formFields && key.toLowerCase() === 'content-type')) {
      curlCommand += ` \\\n  -H "${key}: ${value}"`;
    }
  });
  
  if (formFields && formFields.length > 0) {
    // Use -F for form data fields
    formFields.forEach(field => {
      if (field.isFile) {
        curlCommand += ` \\\n  -F "${field.key}=@/path/to/file"`;
      } else {
        curlCommand += ` \\\n  -F "${field.key}=${field.value}"`;
      }
    });
  } else if (body && method.toUpperCase() !== 'GET') {
    curlCommand += ` \\\n  -d '${body}'`;
  }
  
  return curlCommand;
}

function generateJavaScriptCode(url, method, headers, body, formFields = null) {
  let code = '';
  
  if (formFields && formFields.length > 0) {
    code += 'const formData = new FormData();\n';
    formFields.forEach(field => {
      if (field.isFile) {
        code += `formData.append('${field.key}', fileInput.files[0]); // Replace with your file input\n`;
      } else {
        code += `formData.append('${field.key}', '${field.value}');\n`;
      }
    });
    code += '\n';
    
    // Filter out Content-Type for FormData
    const filteredHeaders = Object.fromEntries(
      Object.entries(headers).filter(([key]) => key.toLowerCase() !== 'content-type')
    );
    const headersObj = Object.keys(filteredHeaders).length > 0 ? JSON.stringify(filteredHeaders, null, 2) : '{}';
    
    code += `const response = await fetch('${url}', {\n  method: '${method.toUpperCase()}',\n  headers: ${headersObj},\n  body: formData\n});`;
  } else {
    const headersObj = JSON.stringify(headers, null, 2);
    code = `const response = await fetch('${url}', {\n  method: '${method.toUpperCase()}',\n  headers: ${headersObj}`;
    
    if (body && method.toUpperCase() !== 'GET') {
      code += `,\n  body: ${JSON.stringify(body)}`;
    }
    
    code += '\n})';
  }
  
  code += ';\n\nconst data = await response.json();\nconsole.log(data);';
  
  return code;
}

function generatePythonCode(url, method, headers, body, formFields = null) {
  let code = 'import requests\n\n';
  code += `url = "${url}"\n`;
  
  if (formFields && formFields.length > 0) {
    // Filter out Content-Type for multipart
    const filteredHeaders = Object.fromEntries(
      Object.entries(headers).filter(([key]) => key.toLowerCase() !== 'content-type')
    );
    if (Object.keys(filteredHeaders).length > 0) {
      code += `headers = ${JSON.stringify(filteredHeaders, null, 2).replace(/"/g, "'")}\n`;
    }
    
    const hasFiles = formFields.some(f => f.isFile);
    if (hasFiles) {
      code += '\nfiles = {\n';
      formFields.forEach((field, i) => {
        if (field.isFile) {
          code += `    '${field.key}': open('/path/to/file', 'rb')`;
        } else {
          code += `    '${field.key}': (None, '${field.value}')`;
        }
        code += i < formFields.length - 1 ? ',\n' : '\n';
      });
      code += '}\n\n';
      code += Object.keys(filteredHeaders).length > 0 
        ? `response = requests.${method.toLowerCase()}(url, headers=headers, files=files)\n`
        : `response = requests.${method.toLowerCase()}(url, files=files)\n`;
    } else {
      code += '\ndata = {\n';
      formFields.forEach((field, i) => {
        code += `    '${field.key}': '${field.value}'`;
        code += i < formFields.length - 1 ? ',\n' : '\n';
      });
      code += '}\n\n';
      code += Object.keys(filteredHeaders).length > 0
        ? `response = requests.${method.toLowerCase()}(url, headers=headers, data=data)\n`
        : `response = requests.${method.toLowerCase()}(url, data=data)\n`;
    }
  } else {
    code += `headers = ${JSON.stringify(headers, null, 2).replace(/"/g, "'")}\n`;
    
    if (body && method.toUpperCase() !== 'GET') {
      code += `data = ${JSON.stringify(body, null, 2).replace(/"/g, "'")}\n\n`;
      code += `response = requests.${method.toLowerCase()}(url, headers=headers, json=data)\n`;
    } else {
      code += `\nresponse = requests.${method.toLowerCase()}(url, headers=headers)\n`;
    }
  }
  
  code += 'print(response.status_code)\nprint(response.json())';
  
  return code;
}

function generateRubyCode(url, method, headers, body, formFields = null) {
  let code = "require 'net/http'\nrequire 'json'\nrequire 'uri'\n";
  
  if (formFields && formFields.length > 0) {
    code += "require 'net/http/post/multipart'\n\n";
    code += `uri = URI('${url}')\n`;
    code += `http = Net::HTTP.new(uri.host, uri.port)\n`;
    
    if (url.startsWith('https')) {
      code += 'http.use_ssl = true\n';
    }
    
    code += `\nrequest = Net::HTTP::${method.charAt(0).toUpperCase() + method.slice(1).toLowerCase()}.new(uri)\n`;
    
    // Filter out Content-Type for multipart
    Object.entries(headers).forEach(([key, value]) => {
      if (key.toLowerCase() !== 'content-type') {
        code += `request['${key}'] = '${value}'\n`;
      }
    });
    
    code += '\n# Note: Install multipart-post gem: gem install multipart-post\n';
    code += 'form_data = {\n';
    formFields.forEach((field, i) => {
      if (field.isFile) {
        code += `  '${field.key}' => UploadIO.new('/path/to/file', 'application/octet-stream')`;
      } else {
        code += `  '${field.key}' => '${field.value}'`;
      }
      code += i < formFields.length - 1 ? ',\n' : '\n';
    });
    code += '}\n';
    code += 'request.set_form form_data, \'multipart/form-data\'\n';
  } else {
    code += "\n";
    code += `uri = URI('${url}')\n`;
    code += `http = Net::HTTP.new(uri.host, uri.port)\n`;
    
    if (url.startsWith('https')) {
      code += 'http.use_ssl = true\n';
    }
    
    code += `\nrequest = Net::HTTP::${method.charAt(0).toUpperCase() + method.slice(1).toLowerCase()}.new(uri)\n`;
    
    Object.entries(headers).forEach(([key, value]) => {
      code += `request['${key}'] = '${value}'\n`;
    });
    
    if (body && method.toUpperCase() !== 'GET') {
      code += `request.body = ${JSON.stringify(body, null, 2).replace(/"/g, "'")}.to_json\n`;
    }
  }
  
  code += '\nresponse = http.request(request)\nputs response.code\nputs response.body';
  
  return code;
}

function generateCSharpCode(url, method, headers, body, formFields = null) {
  let code = 'using System;\nusing System.Net.Http;\n';
  
  if (formFields && formFields.length > 0) {
    code += 'using System.IO;\nusing System.Threading.Tasks;\n\n';
    code += 'class Program\n{\n    static async Task Main(string[] args)\n    {\n';
    code += '        using var client = new HttpClient();\n';
    
    // Filter out Content-Type for multipart
    Object.entries(headers).forEach(([key, value]) => {
      if (key.toLowerCase() !== 'content-type') {
        code += `        client.DefaultRequestHeaders.Add("${key}", "${value}");\n`;
      }
    });
    
    code += `\n        var url = "${url}";\n`;
    code += '        using var formData = new MultipartFormDataContent();\n\n';
    
    formFields.forEach(field => {
      if (field.isFile) {
        code += `        // Add file: formData.Add(new StreamContent(File.OpenRead("/path/to/file")), "${field.key}", "filename");\n`;
      } else {
        code += `        formData.Add(new StringContent("${field.value}"), "${field.key}");\n`;
      }
    });
    
    code += `\n        var response = await client.${method.charAt(0).toUpperCase() + method.slice(1).toLowerCase()}Async(url, formData);\n`;
  } else {
    code += 'using System.Text;\nusing System.Threading.Tasks;\n\n';
    code += 'class Program\n{\n    static async Task Main(string[] args)\n    {\n';
    code += '        using var client = new HttpClient();\n';
    
    Object.entries(headers).forEach(([key, value]) => {
      if (key.toLowerCase() !== 'content-type') {
        code += `        client.DefaultRequestHeaders.Add("${key}", "${value}");\n`;
      }
    });
    
    code += `\n        var url = "${url}";\n`;
    
    if (body && method.toUpperCase() !== 'GET') {
      code += `        var json = ${JSON.stringify(body, null, 8)};\n`;
      code += '        var content = new StringContent(json, Encoding.UTF8, "application/json");\n\n';
      code += `        var response = await client.${method.charAt(0).toUpperCase() + method.slice(1).toLowerCase()}Async(url, content);\n`;
    } else {
      code += `        var response = await client.${method.charAt(0).toUpperCase() + method.slice(1).toLowerCase()}Async(url);\n`;
    }
  }
  
  code += '\n        var responseContent = await response.Content.ReadAsStringAsync();\n';
  code += '        Console.WriteLine($"Status: {response.StatusCode}");\n';
  code += '        Console.WriteLine($"Content: {responseContent}");\n';
  code += '    }\n}';
  
  return code;
}

function buildRequestData(config) {
  const {
    endpoint,
    endpointIdentifier,
    baseUrl,
    customHeaders = {},
    includeAuth = true,
    validateBody = false,
    allowEmptyPathParams = false
  } = config;

  let finalUrl = endpoint ? endpoint.path : baseUrl;
  
  if (endpoint && endpoint.parameters) {
    const pathParams = endpoint.parameters.filter(p => p.source === "Route") || [];
    
    pathParams.forEach(param => {
      let paramValue = '';
      if (endpointIdentifier) {
        paramValue = document.getElementById(`param-${endpointIdentifier}-${param.name}`)?.value;
      }
      
      if (!paramValue && !allowEmptyPathParams) {
        paramValue = `{${param.name}}`;
      }
      
      if (paramValue) {
        finalUrl = finalUrl.replace(`{${param.name}}`, paramValue);
      }
    });
    
    const queryParams = endpoint.parameters.filter(p => p.source === "Query") || [];
    const queryString = new URLSearchParams();
    
    queryParams.forEach(param => {
      if (endpointIdentifier) {
        const paramValue = document.getElementById(`param-${endpointIdentifier}-${param.name}`)?.value;
        if (paramValue) {
          queryString.append(param.name, paramValue);
        }
      }
    });
    
    if (queryString.toString()) {
      finalUrl += (finalUrl.includes('?') ? '&' : '?') + queryString.toString();
    }
  }
  
  if (!finalUrl.startsWith('http')) {
    finalUrl = window.location.origin + (finalUrl.startsWith('/') ? '' : '/') + finalUrl;
  }
  
  const hasFileParams = endpoint && endpoint.parameters && 
    endpoint.parameters.some(p => p.source === "File" || p.isFile);
  
  const hasFormParams = endpoint && endpoint.parameters &&
    endpoint.parameters.some(p => p.source === "Form");
  
  // Use multipart/form-data for file or form parameters (let browser set the boundary)
  const hasFileOrFormParams = hasFileParams || hasFormParams;
  
  const headers = hasFileOrFormParams ? { ...customHeaders } : { 'Content-Type': 'application/json', ...customHeaders };
  
  if (endpoint && endpoint.parameters && endpointIdentifier) {
    const headerParams = endpoint.parameters.filter(p => p.source === "Header") || [];
    headerParams.forEach(param => {
      const paramValue = document.getElementById(`param-${endpointIdentifier}-${param.name}`)?.value;
      if (paramValue) {
        // Use the HeaderName if specified, otherwise use the parameter name
        const headerName = param.headerName || param.name;
        headers[headerName] = paramValue;
      }
    });
  }
  
  if (includeAuth) {
    const authHeaders = getAuthHeaders();
    Object.keys(authHeaders).forEach(key => {
      if (!headers[key]) {
        headers[key] = authHeaders[key];
      }
    });
  }
  
  let requestBody = null;
  const method = endpoint ? endpoint.httpMethodType : 'GET';
  
  if (method !== 'GET' && endpoint && endpointIdentifier) {
    // Handle file uploads or form data with multipart/form-data
    if (hasFileOrFormParams) {
      const formData = new FormData();
      
      // Add file parameters
      const fileParams = endpoint.parameters.filter(p => p.source === "File" || p.isFile);
      fileParams.forEach(param => {
        const fileInput = document.getElementById(`param-file-${endpointIdentifier}-${param.name}`);
        if (fileInput && fileInput.files.length > 0) {
          // Check if this is an array/collection type that accepts multiple files
          const isMultiple = param.type.includes('[]') || 
                           param.type.toLowerCase().includes('list') || 
                           param.type.toLowerCase().includes('collection') ||
                           param.type.toLowerCase().includes('enumerable');
          
          if (isMultiple) {
            // Append all files for array/collection types
            Array.from(fileInput.files).forEach(file => {
              formData.append(param.name, file);
            });
          } else {
            // Append only the first file for single file parameters
            formData.append(param.name, fileInput.files[0]);
          }
        }
      });
      
      // Add form parameters (FromForm)
      const formParams = endpoint.parameters.filter(p => p.source === "Form");
      formParams.forEach(param => {
        // Check if this is a complex type with schema (multiple properties)
        if (param.schema && param.schema.properties && Object.keys(param.schema.properties).length > 0) {
          // Collect values from all property input fields
          const properties = param.schema.properties;
          Object.keys(properties).forEach(propName => {
            const propInput = document.getElementById(`param-${endpointIdentifier}-${param.name}.${propName}`);
            if (propInput) {
              if (propInput.type === 'file') {
                if (propInput.files && propInput.files.length > 0) {
                  formData.append(`${param.name}.${propName}`, propInput.files[0]);
                }
                return;
              } else {
                const value = propInput.value;
                if (value !== '' && value !== null && value !== undefined) {
                  formData.append(`${param.name}.${propName}`, value);
                }
              }
            }
          });
        } else {
          // Simple form parameter
          const paramInput = document.getElementById(`param-${endpointIdentifier}-${param.name}`);
          if (paramInput && paramInput.value) {
            formData.append(param.name, paramInput.value);
          }
        }
      });
      
      // Add other Body parameters as form fields (legacy support)
      const bodyParams = endpoint.parameters.filter(p => p.source === "Body");
      bodyParams.forEach(param => {
        const paramInput = document.getElementById(`param-${endpointIdentifier}-${param.name}`);
        if (paramInput && paramInput.value) {
          formData.append(param.name, paramInput.value);
        }
      });
      
      // Add request body content if exists (for additional JSON data)
      if (endpoint.requestBody) {
        const bodyTextarea = document.getElementById(`request-body-${endpointIdentifier}`);
        const keyValueEditor = document.getElementById(`request-body-kv-${endpointIdentifier}`);
        
        let requestBodyContent = '';
        
        if (keyValueEditor && keyValueEditor.style.display !== 'none') {
          const keyValueData = getKeyValueData(`request-body-kv-${endpointIdentifier}`);
          if (Object.keys(keyValueData).length > 0) {
            requestBodyContent = JSON.stringify(keyValueData);
          }
        } else if (bodyTextarea && bodyTextarea.value.trim()) {
          requestBodyContent = bodyTextarea.value.trim();
        }
        
        if (requestBodyContent) {
          // Add body content as individual form fields
          try {
            const bodyData = JSON.parse(requestBodyContent);
            Object.entries(bodyData).forEach(([key, value]) => {
              formData.append(key, typeof value === 'object' ? JSON.stringify(value) : value);
            });
          } catch (e) {
            // If not JSON, add as single field
            formData.append('data', requestBodyContent);
          }
        }
      }
      
      requestBody = formData;
    } else if (endpoint.requestBody) {
      // Regular JSON body
      const bodyTextarea = document.getElementById(`request-body-${endpointIdentifier}`);
      const keyValueEditor = document.getElementById(`request-body-kv-${endpointIdentifier}`);
      
      let requestBodyContent = '';
      
      if (keyValueEditor && keyValueEditor.style.display !== 'none') {
        const keyValueData = getKeyValueData(`request-body-kv-${endpointIdentifier}`);
        if (Object.keys(keyValueData).length > 0) {
          requestBodyContent = JSON.stringify(keyValueData);
        }
      } else if (bodyTextarea && bodyTextarea.value.trim()) {
        requestBodyContent = bodyTextarea.value.trim();
      }
      
      if (requestBodyContent) {
        if (validateBody) {
          try {
            if (headers['Content-Type'].includes('json')) {
              JSON.parse(requestBodyContent);
            }
          } catch (e) {
            throw new Error('Invalid JSON in request body: ' + e.message);
          }
        }
        requestBody = requestBodyContent;
      }
    }
  }
  
  // Extract FormData entries for export
  let formFields = null;
  if (requestBody instanceof FormData) {
    formFields = [];
    for (const [key, value] of requestBody.entries()) {
      formFields.push({
        key: key,
        value: value,
        isFile: value instanceof File
      });
    }
  }
  
  return {
    url: finalUrl,
    method: method,
    headers: headers,
    body: requestBody,
    formFields: formFields,
    requestOptions: {
      method: method,
      headers: headers,
      ...(requestBody && { body: requestBody })
    }
  };
}

function buildRequestDataForExport(controllerName, endpointIndex) {
  const controller = controllers.find(c => c.name === controllerName);
  if (!controller) return null;
  
  const endpoint = controller.endpoints[endpointIndex];
  if (!endpoint) return null;
  
  const endpointIdentifier = `${controllerName}-${endpointIndex}`;
  
  try {
    const requestData = buildRequestData({
      endpoint: endpoint,
      endpointIdentifier: endpointIdentifier,
      allowEmptyPathParams: true
    });
    
    return {
      ...requestData,
      endpoint: endpoint
    };
  } catch (error) {
    console.error('Error building request data for export:', error);
    return null;
  }
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
  const root = document.documentElement;
  const styles = getComputedStyle(root);
  const good = styles.getPropertyValue('--perf-good').trim();
  const warning = styles.getPropertyValue('--perf-warning').trim();
  const bad = styles.getPropertyValue('--perf-bad').trim();
  
  if (ms < 500) return good;
  if (ms < 1000) return warning;
  return bad;
}

function getSizeColor(bytes) {
  const root = document.documentElement;
  const styles = getComputedStyle(root);
  const good = styles.getPropertyValue('--perf-good').trim();
  const warning = styles.getPropertyValue('--perf-warning').trim();
  const bad = styles.getPropertyValue('--perf-bad').trim();
  
  if (bytes < 1024) return good;
  if (bytes < 1024 * 100) return warning;
  return bad;
}

function generatePerformanceHtml(duration, requestSize, responseSize, status) {
  return `
    <div class="performance-metrics" style="background: var(--bg-secondary); border-radius: 6px; padding: 12px; margin-bottom: 12px; border-left: 4px solid ${getDurationColor(duration)};">
      <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 8px;">
        <h6 style="margin: 0; color: var(--text-primary);">Performance</h6>
      </div>
      <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 8px; font-size: 0.85em;">
        <div><strong>Duration:</strong> <span style="color: ${getDurationColor(duration)}; font-weight: 600;">${formatDuration(duration)}</span></div>
        <div><strong>Request:</strong> <span style="color: ${getSizeColor(requestSize)}; font-weight: 600;">${formatBytes(requestSize)}</span></div>
        <div><strong>Response:</strong> <span style="color: ${getSizeColor(responseSize)}; font-weight: 600;">${formatBytes(responseSize)}</span></div>
        <div><strong>Status:</strong> <span style="color: ${status >= 200 && status < 300 ? getComputedStyle(document.documentElement).getPropertyValue('--perf-good').trim() : getComputedStyle(document.documentElement).getPropertyValue('--perf-bad').trim()}; font-weight: 600;">${status || 'Error'}</span></div>
      </div>
    </div>
  `
}

function escapeHtml(text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function createKeyValueField(containerId, key = '', value = '', config = {}) {
  const fieldsContainer = document.getElementById(containerId);
  
  const fieldRow = document.createElement('div');
  fieldRow.className = config.rowClassName || 'tryout-parameter-row';
  if (config.rowStyle) {
    fieldRow.style.cssText = config.rowStyle;
  } else {
    fieldRow.style.marginBottom = '8px';
  }
  
  let valueStr = value;
  if (typeof value === 'object' && value !== null) {
    valueStr = JSON.stringify(value);
  } else if (typeof value !== 'string') {
    valueStr = String(value);
  }
  
  const escapedKey = escapeHtml(key);
  const escapedValue = escapeHtml(valueStr);
  
  const removeButtonClass = config.removeButtonClass || 'remove-header';
  const removeButtonStyle = config.removeButtonStyle || 'background: var(--btn-danger); color: white; border: none; padding: 6px 10px; border-radius: 4px; cursor: pointer;';
  const removeFunctionName = config.removeFunctionName || 'removeKeyValueField';
  const onChangeHandler = config.onChangeHandler ? `onchange="${config.onChangeHandler}"` : '';
  
  fieldRow.innerHTML = `
    <input type="text" placeholder="Field name" value="${escapedKey}" class="header-input" style="flex: 1; margin-right: 8px;" ${onChangeHandler}>
    <input type="text" placeholder="Field value" value="${escapedValue}" class="header-input" style="flex: 2; margin-right: 8px;" ${onChangeHandler}>
    <button type="button" class="${removeButtonClass}" onclick="${removeFunctionName}(this)" style="${removeButtonStyle}">&times;</button>
  `;
  
  fieldsContainer.appendChild(fieldRow);
}

function populateKeyValueContainer(containerId, data, config = {}) {
  const fieldsContainer = document.getElementById(containerId);
  fieldsContainer.innerHTML = '';
  
  Object.entries(data).forEach(([key, value]) => {
    createKeyValueField(containerId, key, value, config);
  });
  
  if (Object.keys(data).length === 0) {
    createKeyValueField(containerId, '', '', config);
  }
}

function parseKeyValueData(containerId) {
  const fieldsContainer = document.getElementById(containerId);
  const data = {};
  
  Array.from(fieldsContainer.children).forEach(row => {
    const inputs = row.querySelectorAll('input');
    const key = inputs[0].value.trim();
    const value = inputs[1].value.trim();
    
    if (key) {
      try {
        data[key] = JSON.parse(value);
      } catch (e) {
        if (value === 'true') {
          data[key] = true;
        } else if (value === 'false') {
          data[key] = false;
        } else if (value === 'null') {
          data[key] = null;
        } else if (value === '') {
          data[key] = '';
        } else if (!isNaN(value) && !isNaN(parseFloat(value)) && value !== '') {
          data[key] = parseFloat(value);
        } else {
          data[key] = value;
        }
      }
    }
  });
  
  return data;
}

function getInitialTheme() {
  console.log(window.KayaApiExplorerConfig)
  if (window.KayaApiExplorerConfig && window.KayaApiExplorerConfig.defaultTheme) {
    const serverTheme = window.KayaApiExplorerConfig.defaultTheme.toLowerCase()
    if (serverTheme === 'light' || serverTheme === 'dark') {
      const userTheme = localStorage.getItem('theme')
      return userTheme || serverTheme
    }
  }
  
  return localStorage.getItem('theme') || 'light'
}

function initializeTheme() {
  document.documentElement.setAttribute('data-theme', currentTheme)
  updateThemeButton()
}

function toggleTheme() {
  currentTheme = currentTheme === 'light' ? 'dark' : 'light'
  document.documentElement.setAttribute('data-theme', currentTheme)
  localStorage.setItem('theme', currentTheme)
  updateThemeButton()
}

function handleLogoClick() {
  logoClickCount++;
  
  // Reset counter if 1 second has passed since last click
  if (logoClickTimer) {
    clearTimeout(logoClickTimer);
  }
  
  logoClickTimer = setTimeout(() => {
    logoClickCount = 0;
  }, 1000);
  
  // Activate easter egg after 3 clicks
  if (logoClickCount >= 3) {
    logoClickCount = 0;
    activateEasterEgg();
    
    if (logoClickTimer) {
      clearTimeout(logoClickTimer);
    }
  }
}

function activateEasterEgg() {
  // Add fun visual effect
  const brand = document.querySelector('.brand');
  brand.style.animation = 'easterEggPulse 0.6s ease-in-out';
  
  // Cycle to easter egg theme
  if (currentTheme === 'easter-egg') {
    currentTheme = 'light';
  } else {
    currentTheme = 'easter-egg';
  }
  
  document.documentElement.setAttribute('data-theme', currentTheme);
  localStorage.setItem('theme', currentTheme);
  updateThemeButton();
  
  // Remove animation after it plays
  setTimeout(() => {
    brand.style.animation = '';
  }, 600);
}

function updateThemeButton() {
  const themeBtn = document.getElementById('themeToggleBtn')
  const sunIcon = themeBtn.querySelector('.sun-icon')
  const moonIcon = themeBtn.querySelector('.moon-icon')
  const themeText = themeBtn.querySelector('.theme-text')
  
  if (currentTheme === 'dark') {
    sunIcon.style.display = 'block'
    moonIcon.style.display = 'none'
    themeText.textContent = 'Light'
  } else {
    sunIcon.style.display = 'none'
    moonIcon.style.display = 'block'
    themeText.textContent = 'Dark'
  }
}

async function checkOpenApiAvailability() {
    const exportBtn = document.getElementById('exportBtn');
    if (!exportBtn) return;

    const config = window.KayaApiExplorerConfig || {};
    const routePrefix = config.routePrefix || '/kaya';

    try {
        // Prefer an existing Swagger / ASP.NET Core OpenAPI endpoint if available
        const possiblePaths = ['/openapi/v1.json', '/openapi.json', '/swagger/v1/swagger.json'];
        let externalUrl = null;

        for (const path of possiblePaths) {
            try {
                const response = await fetch(path, { method: 'GET' });
                if (response.ok) { externalUrl = path; break; }
            } catch (e) { /* continue */ }
        }

        const targetUrl = externalUrl ?? `${routePrefix}/openapi.json`;
        exportBtn.style.display = 'block';
        exportBtn.addEventListener('click', () => exportOpenApiSpec(targetUrl));
    } catch (error) {
        // Always show using the built-in fallback
        const routePrefix2 = (window.KayaApiExplorerConfig || {}).routePrefix || '/kaya';
        exportBtn.style.display = 'block';
        exportBtn.addEventListener('click', () => exportOpenApiSpec(`${routePrefix2}/openapi.json`));
    }
}

async function exportOpenApiSpec(url) {
    try {
        const response = await fetch(url);
        if (!response.ok) {
            alert('Failed to fetch OpenAPI specification');
            return;
        }
        
        const openApiSpec = await response.json();
        const dataStr = JSON.stringify(openApiSpec, null, 2);
        const dataBlob = new Blob([dataStr], { type: 'application/json' });
        const downloadUrl = URL.createObjectURL(dataBlob);
        const link = document.createElement('a');
        link.href = downloadUrl;
        link.download = 'openapi.json';
        link.click();
        URL.revokeObjectURL(downloadUrl);
    } catch (error) {
        console.error('Error exporting OpenAPI spec:', error);
        alert('Failed to export OpenAPI specification');
    }
}

async function loadApiData() {
  try {
    const config = window.KayaApiExplorerConfig || {};
    const routePrefix = config.routePrefix || '/kaya';
    const response = await fetch(`${routePrefix}/api-docs`);
    const data = await response.json();
    console.log('API data loaded:', data);
    controllers = data.controllers || [];
    if (controllers.length > 0) {
      selectedController = controllers[0].name;
    }
    renderControllers();
    renderEndpoints();
  } catch (error) {
    console.error('Failed to load API data:', error);
    controllers = [];
    selectedController = controllers[0]?.name || "";
    renderControllers();
    renderEndpoints();
  }
}

function getMethodColor(method) {
  switch (method) {
    case "GET":
      return "get"
    case "POST":
      return "post"
    case "PUT":
      return "put"
    case "DELETE":
      return "delete"
    case "PATCH":
      return "patch"
    default:
      return "default"
  }
}

function getStatusClass(code) {
  if (code.startsWith("2")) return "status-2xx"
  if (code.startsWith("4")) return "status-4xx"
  return "status-default"
}

function doesEndpointMatchQuery(endpoint, query) {
  if (!query || !query.trim()) return true
  
  const lowerQuery = query.toLowerCase()
  const endpointPath = endpoint.path.toLowerCase()
  const endpointMethod = endpoint.httpMethodType.toLowerCase()
  const endpointName = endpoint.methodName.toLowerCase()
  const endpointDescription = endpoint.description.toLowerCase()
  
  return endpointPath.includes(lowerQuery) || 
         endpointMethod.includes(lowerQuery) || 
         endpointName.includes(lowerQuery) || 
         endpointDescription.includes(lowerQuery)
}

function renderControllers() {
  const container = document.getElementById("controllersList")
  container.innerHTML = ""
  const query = document.getElementById("searchInput").value.toLowerCase().trim()

  controllers.forEach((controller) => {
    const card = document.createElement("div")
    card.className = `controller-card ${selectedController === controller.name ? "active" : ""}`
    card.onclick = () => selectController(controller.name)

    const badges = controller.endpoints
      .map((endpoint) => `<span class="badge ${getMethodColor(endpoint.httpMethodType)}">${endpoint.httpMethodType}</span>`)
      .join("")

    card.innerHTML = `
            <h3>${controller.name}</h3>
            <p>${controller.description}</p>
            <div class="method-badges">${badges}</div>
        `

    if (query) {
      const hasMatchingEndpoint = controller.endpoints.some(endpoint => 
        doesEndpointMatchQuery(endpoint, query)
      )
      const title = controller.name.toLowerCase()
      const description = controller.description.toLowerCase()
      const hasMatchingController = title.includes(query) || description.includes(query)

      if (!hasMatchingEndpoint && !hasMatchingController) {
        card.style.display = "none"
      }
    }

    container.appendChild(card)
  })
}

function renderEndpoints() {
  const controller = controllers.find((c) => c.name === selectedController)
  if (!controller) return

  document.getElementById("controllerTitle").textContent = controller.name
  document.getElementById("controllerDescription").textContent = controller.description

  const container = document.getElementById("endpointsList")
  
  saveEndpointStates()
  
  container.innerHTML = ""

  const query = document.getElementById("searchInput").value.toLowerCase().trim()
  
  let endpointsToShow = controller.endpoints
  if (query) {
    endpointsToShow = controller.endpoints.filter(endpoint => 
      doesEndpointMatchQuery(endpoint, query)
    )
  }

  endpointsToShow.forEach((endpoint) => {
    const originalIndex = controller.endpoints.indexOf(endpoint)
    const endpointId = `${selectedController}-${originalIndex}`
    const isExpanded = expandedEndpoints.includes(endpointId)

    const card = document.createElement("div")
    card.className = "endpoint-card"

    const authBadge = endpoint.requiresAuthorization 
      ? `<span class="badge auth-badge" title="${endpoint.roles.length > 0 ? 'Requires role(s): ' + endpoint.roles.join(', ') : 'Requires authentication'}">
           <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-right: 4px;">
             <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
             <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
           </svg>
           ${endpoint.roles.length > 0 ? endpoint.roles.join(', ') : 'Auth'}
         </span>`
      : '';

    const obsoleteBadge = endpoint.isObsolete
      ? `<span class="badge obsolete-badge" title="${endpoint.obsoleteMessage ? 'Obsolete: ' + endpoint.obsoleteMessage : 'This endpoint is deprecated'}">
           <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-right: 4px;">
             <circle cx="12" cy="12" r="10"></circle>
             <line x1="12" y1="8" x2="12" y2="12"></line>
             <line x1="12" y1="16" x2="12.01" y2="16"></line>
           </svg>
           Obsolete
         </span>`
      : '';

    card.innerHTML = `
            <div class="endpoint-header" onclick="toggleEndpoint('${endpointId}')">
                <div class="endpoint-title">
                    <div class="endpoint-method-path">
                        <span class="badge ${getMethodColor(endpoint.httpMethodType)}">${endpoint.httpMethodType}</span>
                        <code class="endpoint-path">${endpoint.path}</code>
                        ${authBadge}
                        ${obsoleteBadge}
                    </div>
                    <svg class="chevron ${isExpanded ? "expanded" : ""}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="9,18 15,12 9,6"></polyline>
                    </svg>
                </div>
                <div class="endpoint-info">
                    <h4>${endpoint.methodName}</h4>
                    <p>${endpoint.description}</p>
                </div>
            </div>
            <div class="endpoint-content ${isExpanded ? "expanded" : ""}" id="content-${endpointId}">
                ${renderEndpointTabs(endpoint, endpointId, originalIndex)}
            </div>
        `

    container.appendChild(card)
  })
  
  if (query && endpointsToShow.length === 0) {
    container.innerHTML = '<p class="text-muted" style="text-align: center; padding: 2rem;">No endpoints match your search query.</p>'
  }
  
  restoreEndpointStates()
}

function renderEndpointTabs(endpoint, endpointId, index) {
  const activeTab = endpointActiveTabs[endpointId] || 'parameters';
  
  return `
        <div class="tabs">
            <div class="tab-list">
                <button class="tab-trigger ${activeTab === 'parameters' ? 'active' : ''}" onclick="switchTab(event, '${endpointId}', 'parameters')">Parameters</button>
                <button class="tab-trigger ${activeTab === 'request' ? 'active' : ''}" onclick="switchTab(event, '${endpointId}', 'request')">Request</button>
                <button class="tab-trigger ${activeTab === 'responses' ? 'active' : ''}" onclick="switchTab(event, '${endpointId}', 'responses')">Responses</button>
                <button class="tab-trigger ${activeTab === 'try' ? 'active' : ''}" onclick="switchTab(event, '${endpointId}', 'try')">Try it out</button>
            </div>
            
            <div class="tab-content ${activeTab === 'parameters' ? 'active' : ''}" id="${endpointId}-parameters">
                ${renderParameters(endpoint)}
            </div>
            
            <div class="tab-content ${activeTab === 'request' ? 'active' : ''}" id="${endpointId}-request">
                ${renderRequest(endpoint)}
            </div>
            
            <div class="tab-content ${activeTab === 'responses' ? 'active' : ''}" id="${endpointId}-responses">
                ${renderResponses(endpoint)}
            </div>
            
            <div class="tab-content ${activeTab === 'try' ? 'active' : ''}" id="${endpointId}-try">
                ${renderTryItOut(endpoint, index)}
            </div>
        </div>
    `
}

function renderSchemaProperties(properties, required) {
  return Object.entries(properties).map(([propName, propInfo]) => {
    const isRequired = required && required.includes(propName);
    const propConstraintBadges = renderConstraintBadges(propInfo.constraints);

    let nestedHtml = '';
    if (propInfo.nestedSchema && propInfo.nestedSchema.properties && Object.keys(propInfo.nestedSchema.properties).length > 0) {
      nestedHtml = `<div style="margin-top: 4px; padding-left: 8px; border-left: 2px solid var(--border-primary);">
        ${renderSchemaProperties(propInfo.nestedSchema.properties, propInfo.nestedSchema.required)}
      </div>`;
    }

    return `
      <div style="padding-left: 8px; padding: 3px 0 3px 8px;">
        <div class="parameter-header" style="border-bottom: none;">
          <code class="parameter-name">${propName}</code>
          <span class="badge">${propInfo.type}</span>
          ${isRequired ? '<span class="badge delete">Required</span>' : ''}
          ${propConstraintBadges}
        </div>
        ${nestedHtml}
      </div>`;
  }).join('');
}

function renderConstraintBadges(constraints) {
  if (!constraints) return '';
  const badges = [];
  if (constraints.minLength != null)  badges.push(`<span class="badge constraint-badge">min: ${constraints.minLength}</span>`);
  if (constraints.maxLength != null)  badges.push(`<span class="badge constraint-badge">max: ${constraints.maxLength}</span>`);
  if (constraints.minimum != null)    badges.push(`<span class="badge constraint-badge">&ge; ${constraints.minimum}</span>`);
  if (constraints.maximum != null)    badges.push(`<span class="badge constraint-badge">&le; ${constraints.maximum}</span>`);
  if (constraints.format)             badges.push(`<span class="badge constraint-badge">${constraints.format}</span>`);
  if (constraints.pattern)            badges.push(`<span class="badge constraint-badge" title="${constraints.pattern}">pattern</span>`);
  return badges.join('');
}

function renderParameters(endpoint) {
  if (!endpoint.parameters || endpoint.parameters.length === 0) {
    return '<p class="text-muted">No parameters required</p>'
  }

  return endpoint.parameters
    .map(
      (param) => {
        const isFileParam = param.source === "File" || param.isFile;
        const requiredBadge = param.required ? '<span class="badge delete">Required</span>' : "";
        const fileIcon = isFileParam ? '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="display: inline-block; vertical-align: middle; margin-right: 4px;"><path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"></path><polyline points="13 2 13 9 20 9"></polyline></svg>' : '';
        const constraintBadges = renderConstraintBadges(param.constraints);

        // For complex body / form params, recursively expand schema properties with constraints
        let schemaHtml = '';
        if (param.schema && param.schema.properties && Object.keys(param.schema.properties).length > 0) {
          schemaHtml = `<div style="margin-top: 6px; padding-left: 8px; border-left: 2px solid var(--border-primary);">
            ${renderSchemaProperties(param.schema.properties, param.schema.required)}
          </div>`;
        }
        
        return `
        <div class="parameter-item">
            <div class="parameter-header">
                <code class="parameter-name">${fileIcon}${param.name}</code>
                <span class="badge">${param.type}</span>
                ${requiredBadge}
                ${isFileParam ? '<span class="badge file-upload-badge">File Upload</span>' : ''}
                ${constraintBadges}
            </div>
            <p class="parameter-description">${param.description || (isFileParam ? 'File to upload' : '')}</p>
            ${schemaHtml}
        </div>
    `;
      }
    )
    .join("")
}

function renderRequest(endpoint) {
  if (!endpoint.requestBody) {
    return '<p class="text-muted">No request body required</p>'
  }

  const escapedType = escapeHtml(endpoint.requestBody.type);

  return `
        <div>
            <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 12px;">
                <h4>Request Body</h4>
                <span class="badge">${escapedType}</span>
            </div>
            <div class="code-block">
                <div style="position: absolute; top: 8px; right: 8px; z-index: 1; display: flex; gap: 4px;">
                    <button class="copy-btn" onclick="copyToClipboard(this)" title="Copy to clipboard">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                        </svg>
                    </button>
                    <button class="copy-btn save-btn" onclick="saveToFile(this, 'request-body')" title="Save to file">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
                            <polyline points="17,21 17,13 7,13 7,21"></polyline>
                            <polyline points="7,3 7,8 15,8"></polyline>
                        </svg>
                    </button>
                </div>
                <pre><code>${endpoint.requestBody.example}</code></pre>
            </div>
        </div>
    `
}

function renderResponses(endpoint) {
  if (!endpoint.response) {
    return '<p class="text-muted">No response body returned</p>'
  }

  const escapedType = escapeHtml(endpoint.response.type);

  return `
    <div>
      <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 12px;">
        <h4>Response Body</h4>
        <span class="badge">${escapedType}</span>
      </div>
      <div class="code-block">
        <div style="position: absolute; top: 8px; right: 8px; z-index: 1; display: flex; gap: 4px;">
          <button class="copy-btn" onclick="copyToClipboard(this)" title="Copy to clipboard">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
            </svg>
          </button>
          <button class="copy-btn save-btn" onclick="saveToFile(this, 'response-body')" title="Save to file">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
              <polyline points="17,21 17,13 7,13 7,21"></polyline>
              <polyline points="7,3 7,8 15,8"></polyline>
            </svg>
          </button>
        </div>
        <pre><code>${endpoint.response.example}</code></pre>
      </div>
    </div>
  `
}

function renderTryItOut(endpoint, index) {
  const endpointIdentifier = `${selectedController}-${index}`;
  const parametersSection = renderTryItOutParameters(endpoint, endpointIdentifier);
  const requestBodySection = renderTryItOutRequestBody(endpoint, endpointIdentifier);

  return `
        <div>
            ${parametersSection}
            ${requestBodySection}
            <div style="display: flex; gap: 8px; margin-bottom: 16px;">
                <button class="btn btn-primary" style="flex: 1;" onclick="executeEndpointById('${selectedController}', ${index})">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polygon points="5,3 19,12 5,21 5,3"></polygon>
                    </svg>
                    Execute Request
                </button>
                <button class="btn btn-outline" onclick="showExportModal('${selectedController}', ${index})" title="Export request as code">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="16,16 12,12 8,16"></polyline>
                        <line x1="12" y1="12" x2="12" y2="21"></line>
                        <path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3"></path>
                        <polyline points="16,16 12,12 8,16"></polyline>
                    </svg>
                    Export
                </button>
            </div>
            <div id="tryout-response-${endpointIdentifier}" class="response-container" style="margin-top: 16px; display: none;">
                <!-- Response will be displayed here -->
            </div>
        </div>
    `
}

function renderTryItOutParameters(endpoint, endpointIdentifier) {
  if (!endpoint.parameters || endpoint.parameters.length === 0) {
    return '';
  }

  const queryParams = endpoint.parameters.filter(p => p.source === "Query");
  const routeParams = endpoint.parameters.filter(p => p.source === "Route");
  const headerParams = endpoint.parameters.filter(p => p.source === "Header");
  const fileParams = endpoint.parameters.filter(p => p.source === "File" || p.isFile);
  const formParams = endpoint.parameters.filter(p => p.source === "Form");

  let parametersHtml = '';

  if (queryParams.length > 0) {
    parametersHtml += `
      <div class="tryout-parameter-group">
        <h4>Query Parameters</h4>
        ${queryParams.map(param => `
          <div class="tryout-parameter-row">
            <label class="tryout-parameter-label ${param.required ? 'required' : ''}">${param.name}:</label>
            <input type="text" id="param-${endpointIdentifier}-${param.name}" placeholder="Enter ${param.name}" class="header-input" style="flex: 1;" ${param.defaultValue ? `value="${param.defaultValue}"` : ''}>
            <span class="tryout-parameter-type">${param.type}</span>
          </div>
        `).join('')}
      </div>
    `;
  }

  if (routeParams.length > 0) {
    parametersHtml += `
      <div class="tryout-parameter-group">
        <h4>Path Parameters</h4>
        ${routeParams.map(param => `
          <div class="tryout-parameter-row">
            <label class="tryout-parameter-label ${param.required ? 'required' : ''}">${param.name}:</label>
            <input type="text" id="param-${endpointIdentifier}-${param.name}" placeholder="Enter ${param.name}" class="header-input" style="flex: 1;" ${param.defaultValue ? `value="${param.defaultValue}"` : ''}>
            <span class="tryout-parameter-type">${param.type}</span>
          </div>
        `).join('')}
      </div>
    `;
  }

  if (headerParams.length > 0) {
    parametersHtml += `
      <div class="tryout-parameter-group">
        <h4>Header Parameters</h4>
        ${headerParams.map(param => {
          const displayName = param.headerName || param.name;
          const placeholder = param.headerName ? `${param.headerName} (${param.name})` : param.name;
          return `
          <div class="tryout-parameter-row">
            <label class="tryout-parameter-label ${param.required ? 'required' : ''}">${displayName}:</label>
            <input type="text" id="param-${endpointIdentifier}-${param.name}" placeholder="Enter ${placeholder}" class="header-input" style="flex: 1;" ${param.defaultValue ? `value="${param.defaultValue}"` : ''}>
            <span class="tryout-parameter-type">${param.type}</span>
          </div>
        `;
        }).join('')}
      </div>
    `;
  }

  if (formParams.length > 0) {
    parametersHtml += `
      <div class="tryout-parameter-group">
        <h4>Form Data</h4>
        ${formParams.map((param, paramIndex) => {
          // Check if this is a complex type with schema (multiple properties)
          if (param.schema && param.schema.properties && Object.keys(param.schema.properties).length > 0) {
            // Render fields for each property in the complex type
            const properties = param.schema.properties;
            const paramLabel = formParams.length > 1 ? `<div style="margin: ${paramIndex > 0 ? '16px' : '0'} 0 8px 0; padding-bottom: 8px; border-bottom: 2px solid var(--border-primary); font-weight: 600; color: var(--text-primary);">${param.name} (${param.type})</div>` : '';
            
            return paramLabel + Object.entries(properties).map(([propName, propInfo]) => {
              const isRequired = param.schema.required && param.schema.required.includes(propName);
              const inputType = getInputTypeForProperty(propInfo.type);
              const placeholder = `Enter ${propName}`;
              const defaultVal = propInfo.defaultValue !== null && propInfo.defaultValue !== undefined ? propInfo.defaultValue : '';
              const propConstraints = renderConstraintBadges(propInfo.constraints);
              
              if (inputType === 'file') {
                const inputId = `param-${endpointIdentifier}-${param.name}.${propName}`;
                const infoId = `file-info-${endpointIdentifier}-${param.name}.${propName}`;
                return `
              <div class="tryout-parameter-row" style="align-items: stretch;">
                <label class="tryout-parameter-label ${isRequired ? 'required' : ''}" style="padding-top: 8px;">${propName}:</label>
                <div style="flex: 1; display: flex; flex-direction: column; gap: 4px;">
                  <div class="file-input-wrapper">
                    <input type="file" 
                           id="${inputId}" 
                           class="file-input" 
                           data-form-param="${param.name}"
                           data-property-name="${propName}"
                           onchange="updateFileInputLabel('${inputId}', '${infoId}')">
                    <label for="${inputId}" class="file-input-label" id="label-${inputId}">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
                        <polyline points="17 8 12 3 7 8"/>
                        <line x1="12" y1="3" x2="12" y2="15"/>
                      </svg>
                      <span id="label-text-${inputId}">Select a file to upload</span>
                    </label>
                  </div>
                  <div class="file-input-info" id="${infoId}"></div>
                </div>
                <span class="tryout-parameter-type">${propInfo.type}</span>
              </div>`;
              } else {
                return `
              <div class="tryout-parameter-row">
                <label class="tryout-parameter-label ${isRequired ? 'required' : ''}">${propName}:${propConstraints}</label>
                <input type="${inputType}" 
                       id="param-${endpointIdentifier}-${param.name}.${propName}" 
                       placeholder="${placeholder}" 
                       class="header-input" 
                       style="flex: 1;" 
                       ${defaultVal ? `value="${defaultVal}"` : ''}
                       data-form-param="${param.name}"
                       data-property-name="${propName}">
                <span class="tryout-parameter-type">${propInfo.type}</span>
              </div>`;
              }
            }).join('');
          } else {
            // Simple form parameter - render single input
            return `
          <div class="tryout-parameter-row">
            <label class="tryout-parameter-label ${param.required ? 'required' : ''}">${param.name}:</label>
            <input type="text" id="param-${endpointIdentifier}-${param.name}" placeholder="Enter ${param.name}" class="header-input" style="flex: 1;" ${param.defaultValue ? `value="${param.defaultValue}"` : ''}>
            <span class="tryout-parameter-type">${param.type}</span>
          </div>`;
          }
        }).join('')}
      </div>
    `;
  }

  if (fileParams.length > 0) {
    parametersHtml += `
      <div class="tryout-parameter-group">
        <h4>File Upload</h4>
        ${fileParams.map(param => {
          // Check if this is an array or collection type (multiple files)
          const isMultiple = param.type.includes('[]') || 
                           param.type.toLowerCase().includes('list') || 
                           param.type.toLowerCase().includes('collection') ||
                           param.type.toLowerCase().includes('enumerable');
          const multipleAttr = isMultiple ? 'multiple' : '';
          const helpText = isMultiple ? 'Select one or more files to upload' : 'Select a file to upload';
          const inputId = `param-file-${endpointIdentifier}-${param.name}`;
          const infoId = `file-info-${endpointIdentifier}-${param.name}`;
          
          return `
          <div class="tryout-parameter-row" style="align-items: stretch;">
            <label class="tryout-parameter-label ${param.required ? 'required' : ''}" style="padding-top: 8px;">${param.name}:</label>
            <div style="flex: 1; display: flex; flex-direction: column; gap: 4px;">
              <div class="file-input-wrapper">
                <input type="file" 
                       id="${inputId}" 
                       class="file-input" 
                       ${multipleAttr}
                       onchange="updateFileInputLabel('${inputId}', '${infoId}')">
                <label for="${inputId}" class="file-input-label" id="label-${inputId}">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
                    <polyline points="17 8 12 3 7 8"/>
                    <line x1="12" y1="3" x2="12" y2="15"/>
                  </svg>
                  <span id="label-text-${inputId}">${helpText}</span>
                </label>
              </div>
              <div class="file-input-info" id="${infoId}"></div>
            </div>
            <span class="tryout-parameter-type">${param.type}</span>
          </div>
        `;
        }).join('')}
      </div>
    `;
  }

  return parametersHtml;
}

function renderTryItOutRequestBody(endpoint, endpointIdentifier) {
  if (!endpoint.requestBody) {
    return '';
  }

  const bodyId = `request-body-${endpointIdentifier}`;
  const keyValueId = `request-body-kv-${endpointIdentifier}`;

  return `
    <div class="tryout-parameter-group">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
        <h4>Request Body <span class="badge">${endpoint.requestBody.type}</span></h4>
        <select id="bodyEditorMode-${endpointIdentifier}" class="method-select" onchange="switchTryItOutBodyEditorMode('${bodyId}', '${keyValueId}')">
          <option value="json">JSON Editor</option>
          <option value="keyvalue">Key-Value Editor</option>
        </select>
      </div>
      <textarea id="${bodyId}" 
                placeholder="Enter request body (JSON)" 
                class="body-textarea request-body-editor active" 
                style="width: 100%; height: 160px; font-family: Monaco, monospace;">${endpoint.requestBody.example}</textarea>
      <div id="${keyValueId}" class="request-body-kv-editor" style="display: none;">
        <div id="${keyValueId}-fields"></div>
        <button type="button" class="btn btn-outline" style="margin-top: 8px;" onclick="addRequestBodyField('${keyValueId}')">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="12" y1="5" x2="12" y2="19"></line>
                <line x1="5" y1="12" x2="19" y2="12"></line>
            </svg>
            Add Field
        </button>
      </div>
    </div>
  `;
}

function renderHeaders() {
  const container = document.getElementById("headersList")
  container.innerHTML = ""

  requestHeaders.forEach((header, index) => {
    const row = document.createElement("div")
    row.className = "header-row"
    row.innerHTML = `
            <input type="text" placeholder="Header name" value="${header.key}" class="header-input" onchange="updateHeader(${index}, 'key', this.value)">
            <input type="text" placeholder="Header value" value="${header.value}" class="header-input" onchange="updateHeader(${index}, 'value', this.value)">
            <button class="remove-header" onclick="removeHeader(${index})">&times;</button>
        `
    container.appendChild(row)
  })
}

// Helper functions to save and restore endpoint states
function saveEndpointStates() {
  // Save active tab for each visible endpoint
  document.querySelectorAll('.endpoint-card').forEach(card => {
    const tabList = card.querySelector('.tab-list');
    if (tabList) {
      const activeTabButton = tabList.querySelector('.tab-trigger.active');
      if (activeTabButton) {
        const onClickAttr = activeTabButton.getAttribute('onclick');
        const match = onClickAttr.match(/switchTab\(event, '([^']+)', '([^']+)'\)/);
        if (match) {
          const endpointId = match[1];
          const tabName = match[2];
          endpointActiveTabs[endpointId] = tabName;
        }
      }
    }
  });
  
  // Save response data for each endpoint
  Object.keys(endpointResponses).forEach(endpointId => {
    const responseContainer = document.getElementById(`tryout-response-${endpointId}`);
    if (responseContainer) {
      endpointResponses[endpointId] = {
        html: responseContainer.innerHTML,
        visible: responseContainer.style.display !== 'none'
      };
    }
  });
}

function restoreEndpointStates() {
  // Restore response data for each endpoint
  Object.keys(endpointResponses).forEach(endpointId => {
    const responseContainer = document.getElementById(`tryout-response-${endpointId}`);
    if (responseContainer && endpointResponses[endpointId]) {
      responseContainer.innerHTML = endpointResponses[endpointId].html;
      responseContainer.style.display = endpointResponses[endpointId].visible ? 'block' : 'none';
    }
  });
  
  // Auto-resize textareas in restored content
  setupTextareaAutoResize();
}

// Event handlers
function selectController(controllerName) {
  selectedController = controllerName
  expandedEndpoints = []
  endpointActiveTabs = {}
  endpointResponses = {}
  renderControllers()
  renderEndpoints()
}

function toggleEndpoint(endpointId) {
  const index = expandedEndpoints.indexOf(endpointId)
  if (index > -1) {
    expandedEndpoints.splice(index, 1)
  } else {
    expandedEndpoints.push(endpointId)
  }

  // Directly toggle the DOM without re-rendering, so form inputs are preserved
  const content = document.getElementById(`content-${endpointId}`)
  if (!content) return

  const isExpanded = expandedEndpoints.includes(endpointId)
  content.classList.toggle('expanded', isExpanded)

  const chevron = content.previousElementSibling?.querySelector('.chevron')
  if (chevron) chevron.classList.toggle('expanded', isExpanded)
}

function switchTab(event, endpointId, tabName) {
  const tabList = event.target.parentElement
  tabList.querySelectorAll(".tab-trigger").forEach((trigger) => {
    trigger.classList.remove("active")
  })
  event.target.classList.add("active")

  const tabsContainer = tabList.parentElement
  tabsContainer.querySelectorAll(".tab-content").forEach((content) => {
    content.classList.remove("active")
  })

  document.getElementById(`${endpointId}-${tabName}`).classList.add("active")
    
  endpointActiveTabs[endpointId] = tabName;

  const activeTab = document.getElementById(`${endpointId}-${tabName}`);
  if (activeTab) setupTextareaAutoResize(activeTab);
}

function copyToClipboard(button) {
  const codeBlock = button.parentElement.nextElementSibling;
  
  const text = codeBlock.textContent;
  navigator.clipboard.writeText(text).then(() => {
    button.innerHTML = "✓";
    setTimeout(() => {
      button.innerHTML = `
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                </svg>
            `;
    }, 2000);
  });
}

function addHeader() {
  requestHeaders.push({ key: "", value: "" })
  renderHeaders()
}

function updateFileInputLabel(inputId, infoId) {
  const input = document.getElementById(inputId);
  const label = document.getElementById(`label-${inputId}`);
  const labelText = document.getElementById(`label-text-${inputId}`);
  const infoContainer = document.getElementById(infoId);
  
  if (!input || !label || !labelText || !infoContainer) return;
  
  const files = input.files;
  
  if (files.length === 0) {
    // No files selected - reset to default
    label.classList.remove('has-files');
    labelText.textContent = input.multiple ? 'Select one or more files to upload' : 'Select a file to upload';
    infoContainer.innerHTML = '';
  } else {
    // Files selected - update UI
    label.classList.add('has-files');
    labelText.textContent = files.length === 1 ? files[0].name : `${files.length} files selected`;
    
    // Show file details
    infoContainer.innerHTML = Array.from(files).map((file, index) => `
      <div class="file-input-filename">
        <div style="display: flex; align-items: center; gap: 8px; flex: 1; min-width: 0;">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"/>
            <polyline points="13 2 13 9 20 9"/>
          </svg>
          <span style="flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="${file.name}">${file.name}</span>
          <span style="color: var(--text-secondary); font-size: 12px;">${formatFileSize(file.size)}</span>
        </div>
      </div>
    `).join('');
  }
}

function formatFileSize(bytes) {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

function getInputTypeForProperty(propType) {
  const typeLower = propType.toLowerCase();
  
  if (typeLower.includes('iformfile') || typeLower.includes('formfile')) {
    return 'file';
  }
  
  if (typeLower.includes('int') || typeLower.includes('long') || typeLower.includes('short') || 
      typeLower.includes('byte') || typeLower.includes('decimal') || 
      typeLower.includes('double') || typeLower.includes('float')) {
    return 'number';
  }
  
  return 'text';
}

async function executeEndpointById(controllerName, endpointIndex) {
  const controller = controllers.find(c => c.name === controllerName);
  if (!controller) {
    console.error('Controller not found:', controllerName);
    return;
  }
  
  const endpoint = controller.endpoints[endpointIndex];
  if (!endpoint) {
    console.error('Endpoint not found at index:', endpointIndex);
    return;
  }
  
  const endpointIdentifier = `${controllerName}-${endpointIndex}`;
  await executeEndpoint(endpoint, endpointIdentifier);
}

async function executeEndpoint(endpoint, endpointIdentifier) {
  const responseContainerId = `tryout-response-${endpointIdentifier}`;
  const responseContainer = document.getElementById(responseContainerId);
  
  if (!responseContainer) {
    return;
  }
  
  responseContainer.style.display = 'block';
  responseContainer.innerHTML = '<p>Executing request...</p>';
  
  endpointResponses[endpointIdentifier] = {
    html: '<p>Executing request...</p>',
    visible: true
  };

  const startTime = performance.now();

  try {
    const { url: finalUrl, requestOptions } = buildRequestData({
      endpoint: endpoint,
      endpointIdentifier: endpointIdentifier,
      validateBody: true
    });

    const response = await fetch(finalUrl, requestOptions);
    const responseText = await response.text();
    
    const endTime = performance.now();
    const duration = Math.round(endTime - startTime);
    const requestSize = requestOptions.body ? new Blob([requestOptions.body]).size : 0;
    const responseSize = new Blob([responseText]).size;
    
    let responseData;
    try {
      responseData = JSON.parse(responseText);
    } catch {
      responseData = responseText;
    }

    const statusClass = response.ok ? 'status-2xx' : 'status-4xx';
    
    const responseHtml = `
      <div class="response-success" style="margin-top: 12px;">
        <div class="response-status" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
          <h5>Response</h5>
          <span class="status-badge ${statusClass}">${response.status} ${response.statusText}</span>
        </div>
        <div class="response-headers" style="margin-bottom: 12px;">
          <h6>Response Headers</h6>
          <pre class="response-headers-pre">${Array.from(response.headers.entries()).map(([key, value]) => `${key}: ${value}`).join('\\n')}</pre>
        </div>
        <div class="response-body">
          <h6>Response Body</h6>
          <div style="position: relative;">
            <div style="position: absolute; top: 8px; right: 8px; z-index: 1; display: flex; gap: 4px;">
              <button class="copy-btn" onclick="copyResponseToClipboard(this)" title="Copy to clipboard">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                  <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                </svg>
              </button>
              <button class="copy-btn save-btn" 
                      onclick="saveToFile(this, 'api-response')" 
                      data-endpoint='${JSON.stringify({
                        httpMethodType: endpoint.httpMethodType,
                        path: endpoint.path,
                        methodName: endpoint.methodName
                      })}' 
                      title="Save to file">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
                  <polyline points="17,21 17,13 7,13 7,21"></polyline>
                  <polyline points="7,3 7,8 15,8"></polyline>
                </svg>
              </button>
            </div>
            <pre class="response-body-pre">${typeof responseData === 'object' ? JSON.stringify(responseData, null, 2) : responseData}</pre>
          </div>
        </div>
      </div>
      ${generatePerformanceHtml(duration, requestSize, responseSize, response.status)}
    `;
    
    responseContainer.innerHTML = responseHtml;
    
    endpointResponses[endpointIdentifier] = {
      html: responseHtml,
      visible: true
    };

  } catch (error) {
    const errorHtml = `
      <div class="response-error" style="margin-top: 12px;">
        <h5>Error</h5>
        <p style="color: var(--error-text);">${error.message}</p>
      </div>
    `;    
    responseContainer.innerHTML = errorHtml;
    
    endpointResponses[endpointIdentifier] = {
      html: errorHtml,
      visible: true
    };
  }
}

function copyResponseToClipboard(button) {
  // Find the pre element - it's a sibling of the button container
  const buttonContainer = button.parentElement;
  const preElement = buttonContainer.nextElementSibling;
  const responseBody = preElement ? preElement.textContent : '';
  
  navigator.clipboard.writeText(responseBody).then(() => {
    button.innerHTML = "✓";
    setTimeout(() => {
      button.innerHTML = `
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
        </svg>
      `;
    }, 2000);
  });
}

function saveToFile(button, type, endpointInfo = null) {
  const buttonContainer = button.parentElement;
  let content;
  
  if (buttonContainer.nextElementSibling) {
    // For cases where buttons are in a container (API responses)
    content = buttonContainer.nextElementSibling.textContent;
  } else {
    // For cases where button is directly positioned (static examples) 
    content = button.nextElementSibling.textContent;
  }
  
  if (!endpointInfo && button.dataset.endpoint) {
    try {
      endpointInfo = JSON.parse(button.dataset.endpoint);
    } catch (e) {
      console.warn('Failed to parse endpoint data:', e);
    }
  }
  
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  let filename;
  
  if (endpointInfo) {
    const method = endpointInfo.httpMethodType.toLowerCase();
    const pathName = endpointInfo.path.replace(/[{}/]/g, '').replace(/\//g, '-').replace(/^-+|-+$/g, '');
    const cleanMethodName = endpointInfo.methodName.replace(/[^a-zA-Z0-9]/g, '');
    filename = `${method}-${pathName || cleanMethodName}-${type}-${timestamp}.json`;
  } else {
    filename = `${type}-${timestamp}.json`;
  }
  
  const blob = new Blob([content], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
  
  const originalIcon = button.innerHTML;
  button.innerHTML = "✓";
  setTimeout(() => {
    button.innerHTML = originalIcon;
  }, 2000);
}

function populateKeyValueEditor(keyValueEditorId, data) {
  const config = {
    onChangeHandler: `updateRequestBodyField('${keyValueEditorId}')`,
    removeFunctionName: 'removeRequestBodyField'
  };
  populateKeyValueContainer(`${keyValueEditorId}-fields`, data, config);
}

function addRequestBodyField(keyValueEditorId) {
  addRequestBodyFieldWithValue(keyValueEditorId, '', '');
}

function addRequestBodyFieldWithValue(keyValueEditorId, key = '', value = '') {
  const config = {
    onChangeHandler: `updateRequestBodyField('${keyValueEditorId}')`,
    removeFunctionName: 'removeRequestBodyField'
  };
  createKeyValueField(`${keyValueEditorId}-fields`, key, value, config);
}

function removeRequestBodyField(button, keyValueEditorId) {
  button.parentElement.remove();
}

function getKeyValueData(keyValueEditorId) {
  return parseKeyValueData(`${keyValueEditorId}-fields`);
}

function switchBodyEditorMode() {
  const mode = document.getElementById('bodyEditorMode').value;
  const jsonEditor = document.getElementById('requestBody');
  const keyValueEditor = document.getElementById('requestBodyKvEditor');
  
  if (mode === 'json') {
    if (keyValueEditor.style.display !== 'none') {
      const keyValueData = getRequestBuilderKeyValueData();
      if (Object.keys(keyValueData).length > 0) {
        jsonEditor.value = JSON.stringify(keyValueData, null, 2);
      }
    }
    
    jsonEditor.style.display = 'block';
    keyValueEditor.style.display = 'none';
    // Auto-resize the JSON editor
    autoResizeTextarea(jsonEditor);
  } else {
    try {
      const jsonData = JSON.parse(jsonEditor.value || '{}');
      populateRequestBuilderKeyValueEditor(jsonData);
    } catch (e) {
      populateRequestBuilderKeyValueEditor({});
    }
    
    jsonEditor.style.display = 'none';
    keyValueEditor.style.display = 'block';
  }
}

function switchTryItOutBodyEditorMode(jsonEditorId, keyValueEditorId) {
  const endpointIdentifier = jsonEditorId.replace('request-body-', '');
  const selectElement = document.getElementById(`bodyEditorMode-${endpointIdentifier}`);
  const mode = selectElement.value;
  const jsonEditor = document.getElementById(jsonEditorId);
  const keyValueEditor = document.getElementById(keyValueEditorId);
  
  if (mode === 'json') {
    if (keyValueEditor.style.display !== 'none') {
      const keyValueData = getKeyValueData(keyValueEditorId);
      if (Object.keys(keyValueData).length > 0) {
        jsonEditor.value = JSON.stringify(keyValueData, null, 2);
      }
    }
    
    jsonEditor.style.display = 'block';
    keyValueEditor.style.display = 'none';
    // Auto-resize the JSON editor
    autoResizeTextarea(jsonEditor);
  } else {
    try {
      const jsonData = JSON.parse(jsonEditor.value || '{}');
      populateKeyValueEditor(keyValueEditorId, jsonData);
    } catch (e) {
      populateKeyValueEditor(keyValueEditorId, {});
    }
    
    jsonEditor.style.display = 'none';
    keyValueEditor.style.display = 'block';
  }
}

function populateRequestBuilderKeyValueEditor(data) {
  const config = {
    rowClassName: 'kv-field-row',
    removeButtonClass: 'kv-remove-btn',
    removeFunctionName: 'removeRequestBuilderBodyField'
  };
  populateKeyValueContainer('requestBodyKvFields', data, config);
}

function addRequestBuilderBodyField() {
  addRequestBuilderBodyFieldWithValue('', '');
}

function addRequestBuilderBodyFieldWithValue(key = '', value = '') {
  const config = {
    rowClassName: 'kv-field-row',
    removeButtonClass: 'kv-remove-btn',
    removeFunctionName: 'removeRequestBuilderBodyField'
  };
  createKeyValueField('requestBodyKvFields', key, value, config);
}

function removeRequestBuilderBodyField(button) {
  button.parentElement.remove();
}

function getRequestBuilderKeyValueData() {
  return parseKeyValueData('requestBodyKvFields');
}

function getRequestBuilderBodyContent() {
  const bodyEditorMode = document.getElementById("bodyEditorMode").value;
  const body = document.getElementById("requestBody").value;
  
  if (bodyEditorMode === 'keyvalue') {
    const keyValueData = getRequestBuilderKeyValueData();
    if (Object.keys(keyValueData).length > 0) {
      return JSON.stringify(keyValueData);
    }
  } else if (body) {
    return body;
  }
  
  return '';
}

function updateHeader(index, field, value) {
  requestHeaders[index][field] = value
}

function removeHeader(index) {
  requestHeaders.splice(index, 1)
  renderHeaders()
}

async function sendRequest() {
  const method = document.getElementById("requestMethod").value
  const url = document.getElementById("requestUrl").value
  const sendBtn = document.getElementById("sendRequestBtn")

  sendBtn.textContent = "Sending..."
  sendBtn.disabled = true

  const startTime = performance.now();

  try {
    const customHeaders = {}
    requestHeaders.forEach((header) => {
      if (header.key && header.value) {
        customHeaders[header.key] = header.value
      }
    })

    const { requestOptions } = buildRequestData({
      baseUrl: url,
      customHeaders: customHeaders,
      includeAuth: true
    });

    requestOptions.method = method;

    if (method !== "GET") {
      const requestBodyContent = getRequestBuilderBodyContent();
      if (requestBodyContent) {
        requestOptions.body = requestBodyContent;
      }
    }

    const response = await fetch(url, requestOptions)
    const responseText = await response.text()

    const endTime = performance.now();
    const duration = Math.round(endTime - startTime);
    const requestSize = requestOptions.body ? new Blob([requestOptions.body]).size : 0;
    const responseSize = new Blob([responseText]).size;

    const responseContainer = document.getElementById("responseContainer")

    if (response.ok) {
      let formattedResponse;
      try {
        const jsonData = JSON.parse(responseText);
        formattedResponse = JSON.stringify(jsonData, null, 2);
      } catch (e) {
        formattedResponse = responseText;
      }
      
      responseContainer.innerHTML = `
                <div class="response-success">
                    <div class="response-status">
                        <span class="status-badge ${getStatusClass(response.status.toString())}">${response.status} ${response.statusText}</span>
                    </div>
                    <div class="response-body">
                        <h5>Response Body</h5>
                        <div style="position: relative;">
                            <div style="position: absolute; top: 8px; right: 8px; z-index: 1; display: flex; gap: 4px;">
                                <button class="copy-btn" onclick="copyToClipboard(this)" title="Copy to clipboard">
                                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                        <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                                        <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                                    </svg>
                                </button>
                                <button class="copy-btn save-btn" onclick="saveToFile(this, 'request-builder-response')" title="Save to file">
                                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                        <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
                                        <polyline points="17,21 17,13 7,13 7,21"></polyline>
                                        <polyline points="7,3 7,8 15,8"></polyline>
                                    </svg>
                                </button>
                            </div>
                            <pre style="background-color: var(--bg-tertiary); color: var(--text-primary); padding: 12px; border-radius: 6px; margin: 0; transition: all 0.3s ease;">${formattedResponse}</pre>
                        </div>
                    </div>
                </div>
                ${generatePerformanceHtml(duration, requestSize, responseSize, response.status)}
            `
    } else {
      throw new Error(`${response.status} ${response.statusText}`)
    }

    // Switch to response tab
    document.querySelector('[data-tab="response"]').click()
  } catch (error) {    
    const responseContainer = document.getElementById("responseContainer")
    responseContainer.innerHTML = `
            <div class="response-error">
                <h5>Error</h5>
                <p>${error.message}</p>
            </div>
        `
    document.querySelector('[data-tab="response"]').click()
  }

  sendBtn.textContent = "Send"
  sendBtn.disabled = false
}

function clearSearch() {
  const searchInput = document.getElementById("searchInput")
  const clearBtn = document.getElementById("clearSearchBtn")
  
  searchInput.value = ""
  clearBtn.style.display = "none"
  filterControllers() 
}

function filterControllers() {
  const query = document.getElementById("searchInput").value.toLowerCase()
  const clearBtn = document.getElementById("clearSearchBtn")
  const cards = document.querySelectorAll(".controller-card")
  let visibleControllers = []

  if (query.trim()) {
    clearBtn.style.display = "flex"
  } else {
    clearBtn.style.display = "none"
  }

  cards.forEach((card) => {
    const controllerName = card.querySelector("h3").textContent
    const controller = controllers.find(c => c.name === controllerName)
    
    if (!controller) {
      card.style.display = "none"
      return
    }

    if (!query.trim()) {
      card.style.display = "block"
      visibleControllers.push(controller)
      return
    }

    const hasMatchingEndpoint = controller.endpoints.some(endpoint => 
      doesEndpointMatchQuery(endpoint, query)
    )

    const title = card.querySelector("h3").textContent.toLowerCase()
    const description = card.querySelector("p").textContent.toLowerCase()
    const hasMatchingController = title.includes(query) || description.includes(query)

    if (hasMatchingEndpoint || hasMatchingController) {
      card.style.display = "block"
      visibleControllers.push(controller)
    } else {
      card.style.display = "none"
    }
  })
  
  if (query.trim()) {
    if (visibleControllers.length === 1) {
      if (selectedController !== visibleControllers[0].name) {
        selectedController = visibleControllers[0].name
        expandedEndpoints = []
        renderControllers() 
      }
    } else if (visibleControllers.length > 1) {
      const isCurrentSelectionVisible = visibleControllers.some(c => c.name === selectedController)
      if (!isCurrentSelectionVisible && visibleControllers.length > 0) {
        selectedController = visibleControllers[0].name
        expandedEndpoints = []
        renderControllers() 
      }
    }
  } else {
    if (!selectedController && controllers.length > 0) {
      selectedController = controllers[0].name
      expandedEndpoints = []
      renderControllers()
    }
  }
  
  renderEndpoints()
}

function showExportModal(controllerName, endpointIndex) {
  const requestData = buildRequestDataForExport(controllerName, endpointIndex);
  if (!requestData) {
    alert('Unable to generate export data');
    return;
  }
  
  showGenericExportModal(requestData, 'endpoint');
}

function showRequestBuilderExportModal() {
  const requestData = buildRequestBuilderExportData();
  showGenericExportModal(requestData, 'requestBuilder');
}

function showGenericExportModal(requestData, type = 'endpoint') {
  if (type === 'requestBuilder') {
    window.currentRequestBuilderExportData = requestData;
  } else {
    window.currentExportData = requestData;
  }
  
  updateExportCode('curl', type);
  
  const modalId = type === 'requestBuilder' ? 'requestBuilderExportModal' : 'exportModal';
  document.getElementById(modalId).classList.add('show');
}

function updateExportCode(format, type = 'endpoint') {
  const dataKey = type === 'requestBuilder' ? 'currentRequestBuilderExportData' : 'currentExportData';
  if (!window[dataKey]) return;
  
  const { url, method, headers, body, formFields } = window[dataKey];
  let code = '';

  switch (format) {
    case 'curl':
      code = generateCurlCode(url, method, headers, body, formFields);
      break;
    case 'javascript':
      code = generateJavaScriptCode(url, method, headers, body, formFields);
      break;
    case 'python':
      code = generatePythonCode(url, method, headers, body, formFields);
      break;
    case 'ruby':
      code = generateRubyCode(url, method, headers, body, formFields);
      break;
    case 'csharp':
      code = generateCSharpCode(url, method, headers, body, formFields);
      break;
    default:
      code = 'Format not supported';
  }
  
  const codeContentId = type === 'requestBuilder' ? 'requestBuilderExportCodeContent' : 'exportCodeContent';
  document.getElementById(codeContentId).textContent = code;
  
  const modalId = type === 'requestBuilder' ? 'requestBuilderExportModal' : 'exportModal';
  const modal = document.getElementById(modalId);
  modal.querySelectorAll('.export-tab').forEach(tab => {
    tab.classList.remove('active');
  });
  modal.querySelector(`[data-export-format="${format}"]`).classList.add('active');
}

function copyExportCode(type = 'endpoint') {
  const codeContentId = type === 'requestBuilder' ? 'requestBuilderExportCodeContent' : 'exportCodeContent';
  const buttonId = type === 'requestBuilder' ? 'copyRequestBuilderExportBtn' : 'copyExportBtn';
  
  const codeContent = document.getElementById(codeContentId).textContent;
  navigator.clipboard.writeText(codeContent).then(() => {
    const button = document.getElementById(buttonId);
    const originalText = button.innerHTML;
    button.innerHTML = '✓';
    setTimeout(() => {
      button.innerHTML = originalText;
    }, 2000);
  });
}

function downloadExportCode(type = 'endpoint') {
  const modalId = type === 'requestBuilder' ? 'requestBuilderExportModal' : 'exportModal';
  const codeContentId = type === 'requestBuilder' ? 'requestBuilderExportCodeContent' : 'exportCodeContent';
  const buttonId = type === 'requestBuilder' ? 'downloadRequestBuilderExportBtn' : 'downloadExportBtn';
  
  const modal = document.getElementById(modalId);
  const format = modal.querySelector('.export-tab.active').dataset.exportFormat;
  const codeContent = document.getElementById(codeContentId).textContent;
  
  const extensions = {
    curl: 'sh',
    javascript: 'js',
    python: 'py',
    ruby: 'rb',
    csharp: 'cs'
  };
  
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const prefix = type === 'requestBuilder' ? 'request-builder' : 'api-request';
  const filename = `${prefix}-${format}-${timestamp}.${extensions[format] || 'txt'}`;
  
  const blob = new Blob([codeContent], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
  
  const button = document.getElementById(buttonId);
  const originalText = button.innerHTML;
  button.innerHTML = '✓';
  setTimeout(() => {
    button.innerHTML = originalText;
  }, 2000);
}

function buildRequestBuilderExportData() {
  const method = document.getElementById("requestMethod").value;
  const url = document.getElementById("requestUrl").value;
  
  const customHeaders = { 'Content-Type': 'application/json' };
  requestHeaders.forEach((header) => {
    if (header.key && header.value) {
      customHeaders[header.key] = header.value;
    }
  });
  
  const authHeaders = getAuthHeaders();
  Object.keys(authHeaders).forEach(key => {
    if (!customHeaders[key]) {
      customHeaders[key] = authHeaders[key];
    }
  });
  
  let requestBody = null;
  if (method !== "GET") {
    const requestBodyContent = getRequestBuilderBodyContent();
    if (requestBodyContent) {
      requestBody = requestBodyContent;
    }
  }
  
  return {
    url: url,
    method: method,
    headers: customHeaders,
    body: requestBody
  };
}

function showModal(modalId) {
  const modal = document.getElementById(modalId);
  modal.classList.add("show");
  // Auto-resize textareas in the modal
  setupTextareaAutoResize(modal);
}

function hideModal(modalId) {
  document.getElementById(modalId).classList.remove("show")
}

function switchRequestTab(tabName) {
  document.querySelectorAll(".tab-trigger").forEach((trigger) => {
    trigger.classList.remove("active")
  })

  document.querySelector(`[data-tab="${tabName}"]`).classList.add("active")

  document.querySelectorAll(".tab-content").forEach((content) => {
    content.classList.remove("active")
  })

  document.getElementById(`${tabName}-tab`).classList.add("active")
  
  // Auto-resize textareas in the newly visible tab
  const activeTab = document.getElementById(`${tabName}-tab`);
  if (activeTab) setupTextareaAutoResize(activeTab);
}

// Initialize the application
document.addEventListener("DOMContentLoaded", async () => {
  initializeTheme()
  
  loadAuthConfiguration()
  
  await checkOpenApiAvailability()

  await loadApiData()
  
  renderHeaders()

  // Show SignalR Explorer button if enabled
  const config = window.KayaApiExplorerConfig || {};
  if (config.signalREnabled) {
    const signalRBtn = document.getElementById("signalRDebugBtn");
    if (signalRBtn) {
      signalRBtn.style.display = "flex";
      signalRBtn.addEventListener("click", () => {
        window.location.href = config.signalRRoute || '/signalr-debug';
      });
    }
  }

  document.getElementById("searchInput").addEventListener("input", filterControllers)
  
  // Also add event listener to show/hide clear button on input
  document.getElementById("searchInput").addEventListener("input", function() {
    const clearBtn = document.getElementById("clearSearchBtn")
    if (this.value.trim()) {
      clearBtn.style.display = "flex"
    } else {
      clearBtn.style.display = "none"
    }
  })

  // Easter egg logo click handler
  const brandElement = document.querySelector('.brand')
  if (brandElement) {
    brandElement.addEventListener("click", handleLogoClick)
  }

  document.getElementById("themeToggleBtn").addEventListener("click", toggleTheme)
  document.getElementById("requestBuilderBtn").addEventListener("click", () => showModal("requestBuilderModal"))
  document.getElementById("authorizeBtn").addEventListener("click", () => showModal("authModal"))

  document.getElementById("closeRequestBuilder").addEventListener("click", () => hideModal("requestBuilderModal"))
  document.getElementById("closeAuth").addEventListener("click", () => hideModal("authModal"))
  document.getElementById("closeExport").addEventListener("click", () => hideModal("exportModal"))
  document.getElementById("closeRequestBuilderExport").addEventListener("click", () => hideModal("requestBuilderExportModal"))

  document.getElementById("addHeaderBtn").addEventListener("click", addHeader)
  document.getElementById("sendRequestBtn").addEventListener("click", sendRequest)

  document.getElementById("saveAuthBtn").addEventListener("click", () => saveAuthConfiguration())
  document.getElementById("clearAuthBtn").addEventListener("click", () => clearAuthConfiguration())

  document.querySelectorAll(".tab-trigger").forEach((trigger) => {
    trigger.addEventListener("click", (e) => {
      const tabName = e.target.getAttribute("data-tab")
      if (tabName) {
        switchRequestTab(tabName)
      }
    })
  })

  document.querySelectorAll(".modal").forEach((modal) => {
    modal.addEventListener("click", (e) => {
      if (e.target === modal) {
        modal.classList.remove("show")
      }
    })
  })
})
