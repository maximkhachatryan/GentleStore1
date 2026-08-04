import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  App as AntApp,
  Button,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import type { Product } from '@gentlestore/shared';
import { resolveAssetUrl } from '@gentlestore/shared';
import { api, API_URL } from '../../api';
import ImageUpload from '../../components/ImageUpload';

interface ProductValues {
  name: string;
  categoryId: string;
  price: number;
  stockQuantity: number;
  isAvailable: boolean;
  displayOrder: number;
  description?: string;
  tagIds?: string[];
}

export default function ProductsPage() {
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [categoryFilter, setCategoryFilter] = useState<string | undefined>();
  const [form] = Form.useForm<ProductValues>();

  const { data: products, isLoading } = useQuery({
    queryKey: ['backoffice', 'products', categoryFilter ?? 'all'],
    queryFn: () => api.backoffice.listProducts({ categoryId: categoryFilter }),
  });
  const { data: categories } = useQuery({ queryKey: ['backoffice', 'categories'], queryFn: () => api.backoffice.listCategories() });
  const { data: tags } = useQuery({ queryKey: ['backoffice', 'tags'], queryFn: () => api.backoffice.listTags() });

  const { data: editingDetail, refetch: refetchEditing } = useQuery({
    queryKey: ['backoffice', 'product', editing?.id],
    queryFn: () => api.backoffice.getProduct(editing!.id),
    enabled: !!editing,
  });

  const invalidateList = () => qc.invalidateQueries({ queryKey: ['backoffice', 'products'] });
  const categoryOptions = useMemo(() => (categories ?? []).map((c) => ({ label: c.name, value: c.id })), [categories]);
  const tagOptions = useMemo(() => (tags ?? []).map((t) => ({ label: t.name, value: t.id })), [tags]);

  const save = useMutation({
    mutationFn: (v: ProductValues) => {
      const payload = {
        name: v.name,
        categoryId: v.categoryId,
        description: v.description,
        price: v.price,
        stockQuantity: v.stockQuantity,
        isAvailable: v.isAvailable,
        displayOrder: v.displayOrder,
        tagIds: v.tagIds ?? [],
      };
      return editing ? api.backoffice.updateProduct(editing.id, payload) : api.backoffice.createProduct(payload);
    },
    onSuccess: (saved) => {
      message.success('Product saved');
      invalidateList();
      if (!editing) setEditing(saved);
    },
    onError: () => message.error('Could not save product'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteProduct(id),
    onSuccess: () => {
      message.success('Product deleted');
      invalidateList();
    },
    onError: () => message.error('Could not delete product'),
  });

  const addImage = useMutation({
    mutationFn: (url: string) => api.backoffice.addProductImage(editing!.id, { imageUrl: url }),
    onSuccess: () => {
      refetchEditing();
      invalidateList();
    },
  });

  const deleteImage = useMutation({
    mutationFn: (imageId: string) => api.backoffice.deleteProductImage(editing!.id, imageId),
    onSuccess: () => {
      refetchEditing();
      invalidateList();
    },
  });

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({
      isAvailable: true,
      stockQuantity: 0,
      price: 0,
      displayOrder: (products?.length ?? 0) + 1,
      tagIds: [],
    });
    setOpen(true);
  };

  const openEdit = (p: Product) => {
    setEditing(p);
    form.setFieldsValue({
      name: p.name,
      categoryId: p.categoryId,
      description: p.description ?? '',
      price: p.price,
      stockQuantity: p.stockQuantity,
      isAvailable: p.isAvailable,
      displayOrder: p.displayOrder,
      tagIds: p.tags.map((t) => t.id),
    });
    setOpen(true);
  };

  const closeModal = () => {
    setOpen(false);
    setEditing(null);
  };

  const columns: ColumnsType<Product> = [
    {
      title: '',
      key: 'image',
      width: 60,
      render: (_, r) => {
        const url = resolveAssetUrl(API_URL, r.images[0]?.imageUrl);
        return url ? (
          <img src={url} alt="" style={{ width: 44, height: 44, objectFit: 'cover', borderRadius: 6 }} />
        ) : (
          <div style={{ width: 44, height: 44, borderRadius: 6, background: '#f5f5f5' }} />
        );
      },
    },
    { title: 'Name', dataIndex: 'name', key: 'name' },
    { title: 'Category', dataIndex: 'categoryName', key: 'categoryName' },
    { title: 'Price', key: 'price', render: (_, r) => r.price.toFixed(2) },
    { title: 'Stock', dataIndex: 'stockQuantity', key: 'stock' },
    { title: 'Available', key: 'available', render: (_, r) => (r.isAvailable ? <Tag color="green">Yes</Tag> : <Tag>No</Tag>) },
    { title: 'Tags', key: 'tags', render: (_, r) => r.tags.map((t) => <Tag key={t.id}>{t.name}</Tag>) },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, r) => (
        <Space>
          <Button size="small" onClick={() => openEdit(r)}>
            Edit
          </Button>
          <Popconfirm title="Delete this product?" onConfirm={() => remove.mutate(r.id)}>
            <Button size="small" danger>
              Delete
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  const images = editingDetail?.images ?? [];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16, gap: 12, flexWrap: 'wrap' }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Products
        </Typography.Title>
        <Space>
          <Select
            allowClear
            placeholder="All categories"
            style={{ width: 200 }}
            options={categoryOptions}
            value={categoryFilter}
            onChange={setCategoryFilter}
          />
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!categories?.length}>
            New product
          </Button>
        </Space>
      </div>
      {!categories?.length && (
        <Typography.Paragraph type="warning">Create a category first before adding products.</Typography.Paragraph>
      )}

      <Table rowKey="id" loading={isLoading} dataSource={products} columns={columns} pagination={false} />

      <Modal
        title={editing ? 'Edit product' : 'New product'}
        open={open}
        onCancel={closeModal}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
        width={640}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label="Name" rules={[{ required: true, message: 'Name is required' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="categoryId" label="Category" rules={[{ required: true, message: 'Category is required' }]}>
            <Select options={categoryOptions} placeholder="Select category" />
          </Form.Item>
          <Space size="large" style={{ display: 'flex' }}>
            <Form.Item name="price" label="Price" rules={[{ required: true }]}>
              <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="stockQuantity" label="Stock" rules={[{ required: true }]}>
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="displayOrder" label="Order" rules={[{ required: true }]}>
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>
          </Space>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item name="tagIds" label="Tags">
            <Select mode="multiple" options={tagOptions} placeholder="Add tags" />
          </Form.Item>
          <Form.Item name="isAvailable" label="Available to customers" valuePropName="checked">
            <Switch />
          </Form.Item>

          <Typography.Text strong>Images</Typography.Text>
          {!editing ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4 }}>
              Save the product first, then add images.
            </Typography.Paragraph>
          ) : (
            <div style={{ marginTop: 8 }}>
              <Space wrap>
                {images.map((img) => (
                  <div key={img.id} style={{ position: 'relative' }}>
                    <img
                      src={resolveAssetUrl(API_URL, img.imageUrl)}
                      alt=""
                      style={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 8, border: '1px solid #f0f0f0' }}
                    />
                    <Button
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                      style={{ position: 'absolute', top: -8, right: -8 }}
                      onClick={() => deleteImage.mutate(img.id)}
                    />
                  </div>
                ))}
              </Space>
              <div style={{ marginTop: 8 }}>
                <ImageUpload onChange={(url) => addImage.mutate(url)} buttonText="Add image" />
              </div>
            </div>
          )}
        </Form>
      </Modal>
    </div>
  );
}
