import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  App as AntApp,
  Button,
  Card,
  Divider,
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
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import type { Product, ProductVariant } from '@gentlestore/shared';
import { resolveAssetUrl } from '@gentlestore/shared';
import { api, API_URL } from '../../api';
import ImageUpload from '../../components/ImageUpload';

interface ProductValues {
  name: string;
  categoryId: string;
  price?: number | null;
  isAvailable: boolean;
  displayOrder: number;
  description?: string;
  tagIds?: string[];
}

export default function ProductsPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [categoryFilter, setCategoryFilter] = useState<string | undefined>();
  const [form] = Form.useForm<ProductValues>();
  const [variantEditorOpen, setVariantEditorOpen] = useState(false);
  const [editingVariant, setEditingVariant] = useState<ProductVariant | null>(null);
  const [vSku, setVSku] = useState('');
  const [vPrice, setVPrice] = useState(0);
  const [vAvailable, setVAvailable] = useState(true);
  const [vOrder, setVOrder] = useState(1);
  const [vSelections, setVSelections] = useState<Record<string, string | undefined>>({});

  const { data: products, isLoading } = useQuery({
    queryKey: ['backoffice', 'products', categoryFilter ?? 'all'],
    queryFn: () => api.backoffice.listProducts({ categoryId: categoryFilter }),
  });
  const { data: categories } = useQuery({ queryKey: ['backoffice', 'categories'], queryFn: () => api.backoffice.listCategories() });
  const { data: tags } = useQuery({ queryKey: ['backoffice', 'tags'], queryFn: () => api.backoffice.listTags() });
  const { data: attributes } = useQuery({
    queryKey: ['backoffice', 'variant-attributes'],
    queryFn: () => api.backoffice.listVariantAttributes(),
  });

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
        price: v.price ?? null,
        isAvailable: v.isAvailable,
        displayOrder: v.displayOrder,
        tagIds: v.tagIds ?? [],
      };
      return editing ? api.backoffice.updateProduct(editing.id, payload) : api.backoffice.createProduct(payload);
    },
    onSuccess: (saved) => {
      message.success(t('products.saved'));
      invalidateList();
      if (!editing) setEditing(saved);
    },
    onError: () => message.error(t('products.saveError')),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteProduct(id),
    onSuccess: () => {
      message.success(t('products.deleted'));
      invalidateList();
    },
    onError: () => message.error(t('products.deleteError')),
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
      price: null,
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
      isAvailable: p.isAvailable,
      displayOrder: p.displayOrder,
      tagIds: p.tags.map((t) => t.id),
    });
    setOpen(true);
  };

  const saveVariant = useMutation({
    mutationFn: () => {
      const payload = {
        sku: vSku.trim() ? vSku.trim() : null,
        price: vPrice,
        isAvailable: vAvailable,
        displayOrder: vOrder,
        optionIds: Object.values(vSelections).filter((id): id is string => !!id),
      };
      return editingVariant
        ? api.backoffice.updateProductVariant(editing!.id, editingVariant.id, payload)
        : api.backoffice.createProductVariant(editing!.id, payload);
    },
    onSuccess: () => {
      message.success(t('products.variantSaved'));
      refetchEditing();
      invalidateList();
      setVariantEditorOpen(false);
    },
    onError: () => message.error(t('products.variantSaveError')),
  });

  const deleteVariant = useMutation({
    mutationFn: (variantId: string) => api.backoffice.deleteProductVariant(editing!.id, variantId),
    onSuccess: () => {
      message.success(t('products.variantDeleted'));
      refetchEditing();
      invalidateList();
    },
    onError: () => message.error(t('products.variantDeleteError')),
  });

  const openVariantEditor = (variant?: ProductVariant) => {
    setEditingVariant(variant ?? null);
    setVSku(variant?.sku ?? '');
    setVPrice(variant?.price ?? (form.getFieldValue('price') ?? 0));
    setVAvailable(variant?.isAvailable ?? true);
    setVOrder(variant?.displayOrder ?? (editingDetail?.variants.length ?? 0) + 1);
    setVSelections(
      (variant?.attributes ?? []).reduce<Record<string, string | undefined>>((acc, a) => {
        if (a.definitionId && a.optionId) acc[a.definitionId] = a.optionId;
        return acc;
      }, {}),
    );
    setVariantEditorOpen(true);
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
    { title: t('common.name'), dataIndex: 'name', key: 'name' },
    { title: t('products.category'), dataIndex: 'categoryName', key: 'categoryName' },
    { title: t('products.price'), key: 'price', render: (_, r) => (r.price === null ? <Typography.Text type="secondary">{t('products.noPrice')}</Typography.Text> : r.price.toFixed(2)) },
    { title: t('products.available'), key: 'available', render: (_, r) => (r.isAvailable ? <Tag color="green">{t('common.yes')}</Tag> : <Tag>{t('common.no')}</Tag>) },
    { title: t('products.tags'), key: 'tags', render: (_, r) => r.tags.map((t2) => <Tag key={t2.id}>{t2.name}</Tag>) },
    {
      title: t('common.actions'),
      key: 'actions',
      render: (_, r) => (
        <Space>
          <Button size="small" onClick={() => openEdit(r)}>
            {t('common.edit')}
          </Button>
          <Popconfirm title={t('products.deleteConfirm')} onConfirm={() => remove.mutate(r.id)}>
            <Button size="small" danger>
              {t('common.delete')}
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
          {t('products.title')}
        </Typography.Title>
        <Space>
          <Select
            allowClear
            placeholder={t('products.allCategories')}
            style={{ width: 200 }}
            options={categoryOptions}
            value={categoryFilter}
            onChange={setCategoryFilter}
          />
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!categories?.length}>
            {t('products.new')}
          </Button>
        </Space>
      </div>
      {!categories?.length && (
        <Typography.Paragraph type="warning">{t('products.createCategoryFirst')}</Typography.Paragraph>
      )}

      <Table rowKey="id" loading={isLoading} dataSource={products} columns={columns} pagination={false} />

      <Modal
        title={editing ? t('products.edit') : t('products.new')}
        open={open}
        onCancel={closeModal}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
        width={640}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label={t('common.name')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="categoryId" label={t('products.category')} rules={[{ required: true, message: t('products.categoryRequired') }]}>
            <Select options={categoryOptions} placeholder={t('products.selectCategory')} />
          </Form.Item>
          <Space size="large" style={{ display: 'flex' }}>
            <Form.Item name="price" label={t('products.price')} extra={t('products.priceOptionalHint')}>
              <InputNumber min={0} step={0.01} placeholder={t('products.noPrice')} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="displayOrder" label={t('products.order')} rules={[{ required: true }]}>
              <InputNumber min={0} style={{ width: '100%' }} />
            </Form.Item>
          </Space>
          <Form.Item name="description" label={t('common.description')}>
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item name="tagIds" label={t('products.tags')}>
            <Select mode="multiple" options={tagOptions} placeholder={t('products.addTags')} />
          </Form.Item>
          <Form.Item name="isAvailable" label={t('products.availableToCustomers')} valuePropName="checked">
            <Switch />
          </Form.Item>

          <Typography.Text strong>{t('products.images')}</Typography.Text>
          {!editing ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4 }}>
              {t('products.saveFirst')}
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
                <ImageUpload onChange={(url) => addImage.mutate(url)} buttonText={t('products.addImage')} />
              </div>
            </div>
          )}

          <Divider />
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography.Text strong>{t('products.variants')}</Typography.Text>
            <Button
              size="small"
              icon={<PlusOutlined />}
              onClick={() => openVariantEditor()}
              disabled={!editing || !attributes?.length}
            >
              {t('products.addVariant')}
            </Button>
          </div>
          {!editing ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4 }}>
              {t('products.saveFirst')}
            </Typography.Paragraph>
          ) : !attributes?.length ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4 }}>
              {t('products.defineAttributesFirst')}
            </Typography.Paragraph>
          ) : (editingDetail?.variants.length ?? 0) === 0 ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 4 }}>
              {t('products.noVariants')}
            </Typography.Paragraph>
          ) : (
            <Space direction="vertical" style={{ width: '100%', marginTop: 8 }} size={8}>
              {(editingDetail?.variants ?? []).map((v) => (
                <Card key={v.id} size="small" styles={{ body: { padding: 12 } }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
                    <Space wrap>
                      {v.attributes.map((a) => (
                        <Tag key={a.definitionId ?? a.name}>
                          {a.name}: {a.value}
                        </Tag>
                      ))}
                      <Typography.Text strong>{v.price.toFixed(2)}</Typography.Text>
                      {v.sku && <Typography.Text type="secondary">{v.sku}</Typography.Text>}
                      {!v.isAvailable && <Tag>{t('common.no')}</Tag>}
                    </Space>
                    <Space>
                      <Button size="small" icon={<EditOutlined />} onClick={() => openVariantEditor(v)} />
                      <Popconfirm title={t('products.deleteVariantConfirm')} onConfirm={() => deleteVariant.mutate(v.id)}>
                        <Button size="small" danger icon={<DeleteOutlined />} />
                      </Popconfirm>
                    </Space>
                  </div>
                </Card>
              ))}
            </Space>
          )}
        </Form>
      </Modal>

      <Modal
        title={editingVariant ? t('products.editVariant') : t('products.addVariant')}
        open={variantEditorOpen}
        onCancel={() => setVariantEditorOpen(false)}
        onOk={() => saveVariant.mutate()}
        confirmLoading={saveVariant.isPending}
        destroyOnHidden
      >
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          {(attributes ?? []).map((def) => (
            <div key={def.id}>
              <div style={{ fontSize: 12, color: '#888', marginBottom: 4 }}>{def.name}</div>
              <Select
                allowClear
                style={{ width: '100%' }}
                placeholder={def.name}
                value={vSelections[def.id]}
                onChange={(val) => setVSelections((s) => ({ ...s, [def.id]: val }))}
                options={def.options.map((o) => ({ label: o.value, value: o.id }))}
              />
            </div>
          ))}
          <Space size="large" wrap align="end">
            <div>
              <div style={{ fontSize: 12, color: '#888', marginBottom: 4 }}>{t('products.price')}</div>
              <InputNumber min={0} step={0.01} value={vPrice} onChange={(val) => setVPrice(val ?? 0)} />
            </div>
            <div>
              <div style={{ fontSize: 12, color: '#888', marginBottom: 4 }}>{t('products.order')}</div>
              <InputNumber min={0} value={vOrder} onChange={(val) => setVOrder(val ?? 0)} />
            </div>
            <div>
              <div style={{ fontSize: 12, color: '#888', marginBottom: 4 }}>{t('products.sku')}</div>
              <Input value={vSku} onChange={(e) => setVSku(e.target.value)} />
            </div>
            <div>
              <div style={{ fontSize: 12, color: '#888', marginBottom: 4 }}>{t('products.available')}</div>
              <Switch checked={vAvailable} onChange={setVAvailable} />
            </div>
          </Space>
        </Space>
      </Modal>
    </div>
  );
}
