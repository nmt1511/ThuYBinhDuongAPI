# ThuYBinhDuong Veterinary Clinic API - React Native Integration Guide

## Tổng quan
API quản lý phòng khám thú y ThuYBinhDuong được xây dựng với ASP.NET Core 9.0, cung cấp các chức năng cho khách hàng và quản trị viên.

**Base URL**: `https://localhost:7001/api`

## Authentication

### JWT Token
Tất cả API (trừ login/register) yêu cầu JWT token trong header:
```javascript
headers: {
  'Authorization': `Bearer ${token}`,
  'Content-Type': 'application/json'
}
```

### Roles
- **0**: Customer (Khách hàng)
- **1**: Administrator (Quản trị viên)

## 1. User Management (Quản lý tài khoản)

### 1.1 Đăng ký tài khoản
**Endpoint**: `POST /api/user/register`

**Request Body**:
```json
{
  "username": "khachhang01",
  "password": "123456",
  "email": "customer@example.com",
  "phoneNumber": "0123456789",
  "role": 0,
  "customerName": "Nguyễn Văn A",
  "address": "123 Đường ABC, Quận 1, TP.HCM",
  "gender": 0
}
```

**Response** (201 Created):
```json
{
  "userId": 1,
  "username": "khachhang01",
  "email": "customer@example.com",
  "phoneNumber": "0123456789",
  "role": 0,
  "createdAt": "2024-01-15T10:30:00Z",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**React Native Example**:
```javascript
const register = async (userData) => {
  try {
    const response = await fetch(`${BASE_URL}/user/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(userData),
    });
    
    const data = await response.json();
    
    if (response.ok) {
      // Lưu token
      await AsyncStorage.setItem('userToken', data.token);
      await AsyncStorage.setItem('userInfo', JSON.stringify(data));
      return data;
    } else {
      throw new Error(data.message || 'Đăng ký thất bại');
    }
  } catch (error) {
    console.error('Register error:', error);
    throw error;
  }
};
```

### 1.2 Đăng nhập
**Endpoint**: `POST /api/user/login`

**Request Body**:
```json
{
  "username": "khachhang01",
  "password": "123456"
}
```

**Response** (200 OK):
```json
{
  "userId": 1,
  "username": "khachhang01",
  "email": "customer@example.com",
  "phoneNumber": "0123456789",
  "role": 0,
  "createdAt": "2024-01-15T10:30:00Z",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**React Native Example**:
```javascript
const login = async (username, password) => {
  try {
    const response = await fetch(`${BASE_URL}/user/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ username, password }),
    });
    
    const data = await response.json();
    
    if (response.ok) {
      await AsyncStorage.setItem('userToken', data.token);
      await AsyncStorage.setItem('userInfo', JSON.stringify(data));
      return data;
    } else {
      throw new Error(data.message || 'Đăng nhập thất bại');
    }
  } catch (error) {
    console.error('Login error:', error);
    throw error;
  }
};
```

### 1.3 Lấy thông tin profile
**Endpoint**: `GET /api/user/profile`

**Headers**: Authorization required

**Response** (200 OK):
```json
{
  "userId": 1,
  "username": "khachhang01",
  "email": "customer@example.com",
  "phoneNumber": "0123456789",
  "role": 0,
  "createdAt": "2024-01-15T10:30:00Z"
}
```

## 2. Pet Management (Quản lý thú cưng)

### 2.1 Lấy danh sách thú cưng của khách hàng
**Endpoint**: `GET /api/pet`

**Headers**: Authorization required (Role: 0)

**Response** (200 OK):
```json
[
  {
    "petId": 1,
    "customerId": 1,
    "name": "Milu",
    "species": "Chó",
    "breed": "Golden Retriever",
    "birthDate": "2022-05-15",
    "imageUrl": "https://example.com/pet1.jpg",
    "age": 2,
    "customerName": "Nguyễn Văn A"
  }
]
```

**React Native Example**:
```javascript
const getPets = async () => {
  try {
    const token = await AsyncStorage.getItem('userToken');
    const response = await fetch(`${BASE_URL}/pet`, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
    });
    
    const data = await response.json();
    
    if (response.ok) {
      return data;
    } else {
      throw new Error(data.message || 'Không thể lấy danh sách thú cưng');
    }
  } catch (error) {
    console.error('Get pets error:', error);
    throw error;
  }
};
```

### 2.2 Thêm thú cưng mới
**Endpoint**: `POST /api/pet`

**Headers**: Authorization required (Role: 0)

**Request Body**:
```json
{
  "name": "Milu",
  "species": "Chó",
  "breed": "Golden Retriever",
  "birthDate": "2022-05-15",
  "imageUrl": "https://example.com/pet1.jpg"
}
```

**Response** (201 Created):
```json
{
  "petId": 1,
  "customerId": 1,
  "name": "Milu",
  "species": "Chó",
  "breed": "Golden Retriever",
  "birthDate": "2022-05-15",
  "imageUrl": "https://example.com/pet1.jpg",
  "age": 2,
  "customerName": "Nguyễn Văn A"
}
```

### 2.3 Cập nhật thông tin thú cưng
**Endpoint**: `PUT /api/pet/{id}`

**Headers**: Authorization required (Role: 0)

**Request Body**: Tương tự như thêm mới

**Response** (200 OK):
```json
{
  "message": "Cập nhật thông tin thú cưng thành công"
}
```

### 2.4 Xóa thú cưng
**Endpoint**: `DELETE /api/pet/{id}`

**Headers**: Authorization required (Role: 0)

**Response** (200 OK):
```json
{
  "message": "Xóa thú cưng thành công"
}
```

**Note**: Không thể xóa thú cưng có lịch hẹn đang chờ hoặc đã xác nhận.

## 3. Appointment Management (Quản lý lịch hẹn)

### 3.1 Lấy danh sách lịch hẹn của khách hàng
**Endpoint**: `GET /api/appointment`

**Headers**: Authorization required (Role: 0)

**Response** (200 OK):
```json
[
  {
    "appointmentId": 1,
    "petId": 1,
    "doctorId": 1,
    "serviceId": 1,
    "appointmentDate": "2024-01-20",
    "appointmentTime": "10:00 AM",
    "weight": 15.5,
    "age": 2,
    "isNewPet": false,
    "status": 0,
    "notes": "Khám định kỳ",
    "createdAt": "2024-01-15T10:30:00Z",
    "petName": "Milu",
    "customerName": "Nguyễn Văn A",
    "doctorName": "BS. Nguyễn Thị B",
    "serviceName": "Khám tổng quát",
    "serviceDescription": "Khám sức khỏe tổng quát cho thú cưng",
    "statusText": "Chờ xác nhận",
    "canCancel": true
  }
]
```

### 3.2 Đặt lịch hẹn mới
**Endpoint**: `POST /api/appointment`

**Headers**: Authorization required (Role: 0)

**Request Body**:
```json
{
  "petId": 1,
  "serviceId": 1,
  "doctorId": 1,
  "appointmentDate": "2024-01-20",
  "appointmentTime": "10:00 AM",
  "weight": 15.5,
  "age": 2,
  "isNewPet": false,
  "notes": "Khám định kỳ"
}
```

**Response** (201 Created): Tương tự như GET appointment

**React Native Example**:
```javascript
const createAppointment = async (appointmentData) => {
  try {
    const token = await AsyncStorage.getItem('userToken');
    const response = await fetch(`${BASE_URL}/appointment`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(appointmentData),
    });
    
    const data = await response.json();
    
    if (response.ok) {
      return data;
    } else {
      throw new Error(data.message || 'Không thể đặt lịch hẹn');
    }
  } catch (error) {
    console.error('Create appointment error:', error);
    throw error;
  }
};
```

### 3.3 Hủy lịch hẹn
**Endpoint**: `DELETE /api/appointment/{id}`

**Headers**: Authorization required (Role: 0)

**Response** (200 OK):
```json
{
  "message": "Hủy lịch hẹn thành công"
}
```

**Note**: Chỉ có thể hủy khi status = 0 (Chờ xác nhận)

### Status Codes:
- **0**: Chờ xác nhận (có thể hủy)
- **1**: Đã xác nhận (không thể hủy)
- **2**: Hoàn thành (không thể hủy)
- **3**: Đã hủy (không thể hủy)

## 4. Doctor API (API bác sĩ cho dropdown)

### 4.1 Lấy danh sách bác sĩ
**Endpoint**: `GET /api/doctor`

**Headers**: Authorization required

**Response** (200 OK):
```json
[
  {
    "doctorId": 1,
    "fullName": "Nguyễn Thị B",
    "specialization": "Nội khoa",
    "experienceYears": 5,
    "branch": "Chi nhánh 1",
    "displayText": "BS. Nguyễn Thị B - Nội khoa (Chi nhánh 1)"
  }
]
```

**React Native Usage**:
```javascript
// Cho dropdown picker
const DoctorPicker = () => {
  const [doctors, setDoctors] = useState([]);
  
  useEffect(() => {
    const fetchDoctors = async () => {
      const doctorList = await getDoctors();
      setDoctors(doctorList);
    };
    fetchDoctors();
  }, []);
  
  return (
    <Picker>
      <Picker.Item label="Chọn bác sĩ (tùy chọn)" value="" />
      {doctors.map(doctor => (
        <Picker.Item 
          key={doctor.doctorId} 
          label={doctor.displayText} 
          value={doctor.doctorId} 
        />
      ))}
    </Picker>
  );
};
```

## 5. Service API (API dịch vụ)

### 5.1 Lấy danh sách dịch vụ cho dropdown
**Endpoint**: `GET /api/service/dropdown?search={searchTerm}`

**Headers**: Authorization required

**Query Parameters**:
- `search` (optional): Tìm kiếm theo tên dịch vụ

**Response** (200 OK):
```json
[
  {
    "serviceId": 1,
    "name": "Khám tổng quát",
    "description": "Khám sức khỏe tổng quát cho thú cưng",
    "price": 500000,
    "duration": 30,
    "category": "Khám bệnh",
    "isActive": true,
    "displayText": "Khám tổng quát - 500,000 VNĐ",
    "priceText": "500,000 VNĐ",
    "durationText": "30 phút"
  }
]
```

### 5.2 Lấy danh sách dịch vụ với phân trang
**Endpoint**: `GET /api/service?search={searchTerm}&category={category}&page={page}&limit={limit}`

**Headers**: Authorization required

**Query Parameters**:
- `search` (optional): Tìm kiếm theo tên, mô tả
- `category` (optional): Lọc theo danh mục
- `page` (default: 1): Trang hiện tại
- `limit` (default: 50): Số lượng mỗi trang

**Response** (200 OK):
```json
{
  "data": [
    {
      "serviceId": 1,
      "name": "Khám tổng quát",
      "description": "Khám sức khỏe tổng quát cho thú cưng",
      "price": 500000,
      "duration": 30,
      "category": "Khám bệnh",
      "isActive": true,
      "displayText": "Khám tổng quát - 500,000 VNĐ",
      "priceText": "500,000 VNĐ",
      "durationText": "30 phút"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 50,
    "total": 25,
    "totalPages": 1
  }
}
```

### 5.3 Lấy danh sách danh mục dịch vụ
**Endpoint**: `GET /api/service/categories`

**Headers**: Authorization required

**Response** (200 OK):
```json
[
  "Khám bệnh",
  "Tiêm phòng",
  "Phẫu thuật",
  "Chăm sóc răng miệng"
]
```

**React Native Example**:
```javascript
const ServicePicker = () => {
  const [services, setServices] = useState([]);
  const [searchTerm, setSearchTerm] = useState('');
  
  const searchServices = async (search) => {
    try {
      const token = await AsyncStorage.getItem('userToken');
      const response = await fetch(
        `${BASE_URL}/service/dropdown?search=${encodeURIComponent(search)}`,
        {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
          },
        }
      );
      
      const data = await response.json();
      if (response.ok) {
        setServices(data);
      }
    } catch (error) {
      console.error('Search services error:', error);
    }
  };
  
  useEffect(() => {
    searchServices(searchTerm);
  }, [searchTerm]);
  
  return (
    <View>
      <TextInput
        placeholder="Tìm kiếm dịch vụ..."
        value={searchTerm}
        onChangeText={setSearchTerm}
      />
      <Picker>
        <Picker.Item label="Chọn dịch vụ" value="" />
        {services.map(service => (
          <Picker.Item 
            key={service.serviceId} 
            label={service.displayText} 
            value={service.serviceId} 
          />
        ))}
      </Picker>
    </View>
  );
};
```

## 6. News API (API tin tức)

### 6.1 Lấy danh sách tin tức
**Endpoint**: `GET /api/news?search={searchTerm}&tag={tag}&page={page}&limit={limit}`

**Headers**: Authorization required

**Query Parameters**:
- `search` (optional): Tìm kiếm theo tiêu đề, nội dung, tags
- `tag` (optional): Lọc theo tag cụ thể  
- `page` (default: 1): Trang hiện tại
- `limit` (default: 10): Số lượng mỗi trang

**Response** (200 OK):
```json
{
  "data": [
    {
      "newsId": 1,
      "title": "Hướng dẫn chăm sóc thú cưng mùa đông",
      "content": "Nội dung đầy đủ của bài viết...",
      "createdAt": "2024-01-15T10:30:00Z",
      "imageUrl": "https://example.com/news1.jpg",
      "tags": "chăm sóc, mùa đông, thú cưng",
      "summary": "Mùa đông đang đến, việc chăm sóc thú cưng cần được quan tâm đặc biệt...",
      "createdAtText": "2 ngày trước",
      "tagList": ["chăm sóc", "mùa đông", "thú cưng"]
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 10,
    "total": 15,
    "totalPages": 2
  }
}
```

### 6.2 Lấy chi tiết tin tức
**Endpoint**: `GET /api/news/{id}`

**Headers**: Authorization required

**Response** (200 OK): Tương tự như item trong danh sách

### 6.3 Lấy danh sách tags
**Endpoint**: `GET /api/news/tags`

**Headers**: Authorization required

**Response** (200 OK):
```json
[
  "chăm sóc",
  "mùa đông", 
  "thú cưng",
  "tiêm phòng",
  "dinh dưỡng"
]
```

**React Native Example**:
```javascript
const NewsList = () => {
  const [news, setNews] = useState([]);
  const [loading, setLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  
  const fetchNews = async (search = '', pageNum = 1) => {
    try {
      setLoading(true);
      const token = await AsyncStorage.getItem('userToken');
      const response = await fetch(
        `${BASE_URL}/news?search=${encodeURIComponent(search)}&page=${pageNum}&limit=10`,
        {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
          },
        }
      );
      
      const data = await response.json();
      if (response.ok) {
        if (pageNum === 1) {
          setNews(data.data);
        } else {
          setNews(prev => [...prev, ...data.data]);
        }
      }
    } catch (error) {
      console.error('Fetch news error:', error);
    } finally {
      setLoading(false);
    }
  };
  
  const renderNewsItem = ({ item }) => (
    <TouchableOpacity onPress={() => navigation.navigate('NewsDetail', { newsId: item.newsId })}>
      <View style={styles.newsItem}>
        <Image source={{ uri: item.imageUrl }} style={styles.newsImage} />
        <View style={styles.newsContent}>
          <Text style={styles.newsTitle}>{item.title}</Text>
          <Text style={styles.newsSummary}>{item.summary}</Text>
          <Text style={styles.newsTime}>{item.createdAtText}</Text>
          <View style={styles.tagsContainer}>
            {item.tagList.map(tag => (
              <Text key={tag} style={styles.tag}>{tag}</Text>
            ))}
          </View>
        </View>
      </View>
    </TouchableOpacity>
  );
  
  return (
    <View>
      <TextInput
        placeholder="Tìm kiếm tin tức..."
        value={searchTerm}
        onChangeText={(text) => {
          setSearchTerm(text);
          setPage(1);
          fetchNews(text, 1);
        }}
      />
      <FlatList
        data={news}
        renderItem={renderNewsItem}
        keyExtractor={item => item.newsId.toString()}
        onEndReached={() => {
          setPage(prev => prev + 1);
          fetchNews(searchTerm, page + 1);
        }}
        refreshing={loading}
        onRefresh={() => {
          setPage(1);
          fetchNews(searchTerm, 1);
        }}
      />
    </View>
  );
};
```

## 7. Error Handling

### Common Error Responses

**400 Bad Request**:
```json
{
  "message": "Dữ liệu không hợp lệ"
}
```

**401 Unauthorized**:
```json
{
  "message": "Token không hợp lệ"
}
```

**403 Forbidden**:
```json
{
  "message": "Bạn không có quyền truy cập"
}
```

**404 Not Found**:
```json
{
  "message": "Không tìm thấy dữ liệu"
}
```

**500 Internal Server Error**:
```json
{
  "message": "Đã xảy ra lỗi hệ thống"
}
```

### React Native Error Handling
```javascript
const apiCall = async (url, options = {}) => {
  try {
    const token = await AsyncStorage.getItem('userToken');
    const response = await fetch(url, {
      ...options,
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        ...options.headers,
      },
    });
    
    const data = await response.json();
    
    if (response.ok) {
      return data;
    } else {
      // Handle different error status codes
      switch (response.status) {
        case 401:
          // Token expired, redirect to login
          await AsyncStorage.removeItem('userToken');
          // Navigate to login screen
          break;
        case 403:
          Alert.alert('Lỗi', 'Bạn không có quyền thực hiện hành động này');
          break;
        case 404:
          Alert.alert('Lỗi', 'Không tìm thấy dữ liệu');
          break;
        default:
          Alert.alert('Lỗi', data.message || 'Đã xảy ra lỗi');
      }
      throw new Error(data.message || 'API call failed');
    }
  } catch (error) {
    console.error('API call error:', error);
    throw error;
  }
};
```

## 8. Complete React Native Example

### API Service Class
```javascript
import AsyncStorage from '@react-native-async-storage/async-storage';

class ApiService {
  constructor() {
    this.baseURL = 'https://localhost:7001/api';
  }
  
  async getToken() {
    return await AsyncStorage.getItem('userToken');
  }
  
  async request(endpoint, options = {}) {
    const token = await this.getToken();
    
    const config = {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...(token && { 'Authorization': `Bearer ${token}` }),
        ...options.headers,
      },
    };
    
    const response = await fetch(`${this.baseURL}${endpoint}`, config);
    const data = await response.json();
    
    if (!response.ok) {
      throw new Error(data.message || 'API call failed');
    }
    
    return data;
  }
  
  // User APIs
  async login(username, password) {
    const data = await this.request('/user/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    });
    
    await AsyncStorage.setItem('userToken', data.token);
    await AsyncStorage.setItem('userInfo', JSON.stringify(data));
    return data;
  }
  
  async register(userData) {
    const data = await this.request('/user/register', {
      method: 'POST',
      body: JSON.stringify(userData),
    });
    
    await AsyncStorage.setItem('userToken', data.token);
    await AsyncStorage.setItem('userInfo', JSON.stringify(data));
    return data;
  }
  
  // Pet APIs
  async getPets() {
    return await this.request('/pet');
  }
  
  async createPet(petData) {
    return await this.request('/pet', {
      method: 'POST',
      body: JSON.stringify(petData),
    });
  }
  
  // Appointment APIs
  async getAppointments() {
    return await this.request('/appointment');
  }
  
  async createAppointment(appointmentData) {
    return await this.request('/appointment', {
      method: 'POST',
      body: JSON.stringify(appointmentData),
    });
  }
  
  async cancelAppointment(appointmentId) {
    return await this.request(`/appointment/${appointmentId}`, {
      method: 'DELETE',
    });
  }
  
  // Doctor APIs
  async getDoctors() {
    return await this.request('/doctor');
  }
  
  // Service APIs  
  async getServicesForDropdown(search = '') {
    return await this.request(`/service/dropdown?search=${encodeURIComponent(search)}`);
  }
  
  async getServices(search = '', category = '', page = 1, limit = 10) {
    const params = new URLSearchParams({
      ...(search && { search }),
      ...(category && { category }),
      page: page.toString(),
      limit: limit.toString(),
    });
    
    return await this.request(`/service?${params}`);
  }
  
  // News APIs
  async getNews(search = '', tag = '', page = 1, limit = 10) {
    const params = new URLSearchParams({
      ...(search && { search }),
      ...(tag && { tag }),
      page: page.toString(),
      limit: limit.toString(),
    });
    
    return await this.request(`/news?${params}`);
  }
  
  async getNewsById(newsId) {
    return await this.request(`/news/${newsId}`);
  }
}

export default new ApiService();
```

## 9. Usage Tips

### 9.1 Token Management
- Lưu token trong AsyncStorage sau khi login/register thành công
- Tự động thêm token vào header cho mọi API call
- Xử lý token expiry (401 response) bằng cách redirect về login

### 9.2 Offline Support
- Cache dữ liệu quan trọng (pets, appointments) trong AsyncStorage
- Hiển thị dữ liệu cache khi không có internet
- Đồng bộ dữ liệu khi có kết nối trở lại

### 9.3 Loading States
- Hiển thị loading indicator cho các API call
- Disable buttons để tránh multiple submissions
- Sử dụng pull-to-refresh cho danh sách

### 9.4 Validation
- Validate dữ liệu ở client trước khi gửi API
- Hiển thị error messages phù hợp với từng field
- Format ngày tháng đúng định dạng API (YYYY-MM-DD)

Tài liệu này cung cấp đầy đủ thông tin để tích hợp API với React Native app. Mọi endpoint đều có example code và response format để developers có thể implement dễ dàng. 